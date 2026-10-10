using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;

namespace Scs.Identity.Infrastructure;

/// <summary>Finds an account's clinic from an email before anyone is signed in.</summary>
internal sealed class AccountLookup(IdentityDbContext db)
{
    public async Task<AccountLocation?> FindAsync(string normalizedEmail, CancellationToken cancellation)
    {
        // The one declared crossing: no clinic is established yet, and without one the account
        // table shows nothing. The procedure answers with two identifiers only.
        var rows = await db.Database
            .SqlQuery<AccountLocation>($"EXEC [identity].[usp_FindAccountForSignIn] @Email = {normalizedEmail}")
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        return rows.SingleOrDefault();
    }
}

/// <summary>What sending a link ended in.</summary>
internal enum LinkSendResult
{
    Sent,

    /// <summary>The mail server refused; nothing was stored and the earlier link, if any, still works.</summary>
    NotSent,

    /// <summary>A reset email went to this account too recently, or too many in the last hour.</summary>
    SkippedLimit,
}

/// <summary>A live link, as far as the page that opened it needs to know.</summary>
/// <param name="Email">Shown back to the holder of the link. Personal data.</param>
internal sealed record LinkView(Guid LinkId, string Kind, Guid AccountId, string Email)
{
    public override string ToString() => nameof(LinkView);
}

/// <summary>
/// Emailed password links (spec 004, FR-008 to FR-013). Every method works for the established
/// clinic, under isolation. A link is sent before it is stored, so a failed email changes
/// nothing; storing it replaces the account's earlier link, so at most one is live.
/// </summary>
internal sealed class PasswordLinks(
    IdentityDbContext db,
    IClinicContext clinic,
    PasswordLinkTokens tokens,
    IEmailSender mail,
    TimeProvider clock,
    IOptions<ScsOptions> options)
{
    private AuthOptions Auth => options.Value.Auth;

    /// <summary>The address the email carries. The token is in the fragment, which no server ever sees.</summary>
    public string AddressFor(string token) => $"{Auth.StaffAppBase()}/set-password#t={token}";

    public string SignInAddress => $"{Auth.StaffAppBase()}/sign-in";

    public TimeSpan LifeOf(string kind) =>
        kind == PasswordLinkKinds.Reset ? TimeSpan.FromMinutes(Auth.ResetLinkMinutes) : TimeSpan.FromHours(Auth.InviteLinkHours);

    /// <summary>
    /// Makes a link, emails it, and only then stores it. One account's links are handled one at
    /// a time under a lock named after the account, held for the transaction — an application
    /// lock, not a row lock, so a slow mail server never holds up that account's sign-in.
    /// </summary>
    /// <param name="enforceResetCaps">
    /// For a link the person asked for themselves: at most one a minute and five an hour
    /// (FR-005). A link sent by someone at the clinic is not held back, but counts.
    /// </param>
    /// <param name="message">Builds the email from the link's address.</param>
    public async Task<LinkSendResult> SendAsync(
        Guid accountId, string kind, bool enforceResetCaps, Func<string, EmailMessage> message, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(message);
        var clinicId = clinic.Require();

        var own = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false)
            : null;

        try
        {
            var resource = $"scs:password-link:{accountId:N}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 DECLARE @held int;
                 EXEC @held = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
                 IF @held < 0 THROW 50001, 'The account is busy with another link.', 1;
                 """,
                cancellation).ConfigureAwait(false);

            var now = clock.GetUtcNow();

            if (enforceResetCaps && await OverResetCapsAsync(accountId, now, cancellation).ConfigureAwait(false))
            {
                return LinkSendResult.SkippedLimit;
            }

            var id = Guid.NewGuid();
            var secret = PasswordLinkTokens.NewSecret();
            var token = tokens.Write(new PasswordLinkToken(kind, clinicId, id, secret));

            try
            {
                await mail.SendAsync(message(AddressFor(token)), cancellation).ConfigureAwait(false);
            }
            catch (EmailNotSentException)
            {
                return LinkSendResult.NotSent;
            }

            await ReplaceAllAsync(accountId, cancellation).ConfigureAwait(false);
            db.PasswordLinks.Add(PasswordLink.Issue(id, accountId, kind, PasswordLinkTokens.Hash(secret), now, LifeOf(kind)));
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

            if (own is not null)
            {
                await own.CommitAsync(cancellation).ConfigureAwait(false);
            }

            return LinkSendResult.Sent;
        }
        finally
        {
            if (own is not null)
            {
                await own.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The live link a token names, with its account — or null. Null also when the account can
    /// no longer use this kind of link: a reset link needs an active account, a new-account link
    /// one that was never activated. Nothing here uses the link up.
    /// </summary>
    public async Task<LinkView?> InspectAsync(PasswordLinkToken token, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(token);
        var now = clock.GetUtcNow();

        var found = await (
            from link in db.PasswordLinks.AsNoTracking()
            join account in db.Accounts.AsNoTracking() on link.AccountId equals account.Id
            where link.Id == token.LinkId
            select new { Link = link, account.Email, account.State }).SingleOrDefaultAsync(cancellation).ConfigureAwait(false);

        if (found is null
            || found.Link.Kind != token.Kind
            || !found.Link.IsLive(now)
            || !CryptographicOperations.FixedTimeEquals(PasswordLinkTokens.Hash(token.Secret), found.Link.SecretHash)
            || found.State != StateNeededBy(token.Kind))
        {
            return null;
        }

        return new LinkView(found.Link.Id, found.Link.Kind, found.Link.AccountId, found.Email);
    }

    public static string StateNeededBy(string kind) =>
        kind == PasswordLinkKinds.Reset ? AccountStates.Active : AccountStates.Invited;

    /// <summary>
    /// Uses a link up, in one statement: of two submissions at the same moment exactly one
    /// succeeds, and a link that ran out between opening and submitting is refused.
    /// </summary>
    public async Task<bool> ConsumeAsync(PasswordLinkToken token, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(token);
        var now = clock.GetUtcNow();
        var hash = PasswordLinkTokens.Hash(token.Secret);

        var used = await db.PasswordLinks
            .Where(l => l.Id == token.LinkId && l.Kind == token.Kind && l.SecretHash == hash
                        && l.UsedAtUtc == null && l.ReplacedAtUtc == null && l.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(update => update.SetProperty(l => l.UsedAtUtc, now), cancellation)
            .ConfigureAwait(false);

        return used == 1;
    }

    /// <summary>Makes every link of the account stop working.</summary>
    public async Task ReplaceAllAsync(Guid accountId, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        await db.PasswordLinks
            .Where(l => l.AccountId == accountId && l.UsedAtUtc == null && l.ReplacedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(l => l.ReplacedAtUtc, now), cancellation)
            .ConfigureAwait(false);
    }

    private async Task<bool> OverResetCapsAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellation)
    {
        var hourAgo = now.AddHours(-1);
        var recent = await db.PasswordLinks.AsNoTracking()
            .Where(l => l.AccountId == accountId && l.Kind == PasswordLinkKinds.Reset && l.CreatedAtUtc > hourAgo)
            .Select(l => l.CreatedAtUtc)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        var gapStart = now.AddSeconds(-Auth.ResetEmailGapSeconds);
        return recent.Count >= Auth.ResetEmailsPerHour || recent.Any(sent => sent > gapStart);
    }
}
