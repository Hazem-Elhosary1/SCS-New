using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// A password the system chooses (SH-044): long enough, with one of each kind of character the
/// rules ask for, and without the characters people confuse when a password is read aloud or
/// copied by hand.
/// </summary>
internal static class PasswordGenerator
{
    public const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    public const string Lower = "abcdefghijkmnpqrstuvwxyz";
    public const string Digits = "23456789";
    public const string Symbols = "!@#$%*+-=?";

    private const string Everything = Upper + Lower + Digits + Symbols;

    public static string Generate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, PasswordRules.MinLength);

        var characters = new char[length];
        characters[0] = Pick(Upper);
        characters[1] = Pick(Lower);
        characters[2] = Pick(Digits);
        characters[3] = Pick(Symbols);

        for (var i = 4; i < length; i++)
        {
            characters[i] = Pick(Everything);
        }

        RandomNumberGenerator.Shuffle(characters.AsSpan());
        return new string(characters);
    }

    private static char Pick(string from) => from[RandomNumberGenerator.GetInt32(from.Length)];
}

/// <summary>One request to set a password, by whichever route.</summary>
/// <param name="EventKind">What is recorded for the clinic when it succeeds.</param>
internal sealed record SetPassword(Guid AccountId, string? Password, string EventKind, RequestOrigin Origin)
{
    /// <summary>The link being used, when there is one: it is used up in the same transaction.</summary>
    public PasswordLinkToken? Link { get; init; }

    /// <summary>The password is someone else's choice: mark it "must change" and give it a limited life.</summary>
    public bool Temporary { get; init; }

    /// <summary>
    /// Refuse a password equal to the current one (FR-026). Never set when someone else is
    /// setting the password: they must not be able to test guesses of the owner's.
    /// </summary>
    public bool MustDiffer { get; init; }

    /// <summary>The one session that survives; every other session of the account ends.</summary>
    public Guid? KeepSessionId { get; init; }

    public string? EventDetail { get; init; }

    public Guid? ActorAccountId { get; init; }

    /// <summary>The type only: this carries a password.</summary>
    public override string ToString() => nameof(SetPassword);
}

internal enum SetPasswordResult
{
    Done,

    /// <summary>The link was used, replaced or ran out in the meantime, or its account can no longer use it.</summary>
    LinkInvalid,

    NotFound,

    Disabled,
}

/// <summary>
/// The one routine that sets a password, for all four routes of spec 004 — a reset link, a
/// new-account link, replacing a temporary password, and re-issuing access. In one transaction
/// it writes the new hash, lifts any lock, makes every link of the account stop working, sets or
/// clears the "must change" mark, activates an invited account, ends the account's sessions and
/// records what happened (FR-015, FR-019, FR-037, FR-044). One routine, so the routes cannot
/// drift apart and none can forget a step.
/// </summary>
internal sealed class PasswordSetter(
    IdentityDbContext db,
    IClinicContext clinic,
    PasswordLinks links,
    SessionService sessions,
    TimeProvider clock,
    IOptions<ScsOptions> options)
{
    /// <summary>The field codes of a password that cannot be accepted; empty when it can.</summary>
    public static IReadOnlyList<string> RuleProblems(string? password) =>
        string.IsNullOrEmpty(password) ? [PasswordRules.Required] : PasswordRules.Check(password);

    public async Task<SetPasswordResult> SetAsync(SetPassword command, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(command);
        clinic.Require();

        // Order of refusals: the link (checked by the caller before anything is typed), the
        // rules, then "differs from the current one".
        var problems = RuleProblems(command.Password);
        if (problems.Count > 0)
        {
            throw Refused([.. problems]);
        }

        var password = command.Password!;

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // The account's row is held from here to the commit: two routes setting the same
            // account's password at the same moment take their turns.
            var account = await db.Accounts
                .FromSqlInterpolated($"SELECT * FROM [identity].[StaffAccount] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {command.AccountId}")
                .SingleOrDefaultAsync(cancellation)
                .ConfigureAwait(false);

            if (account is null)
            {
                return SetPasswordResult.NotFound;
            }

            if (account.State == AccountStates.Disabled)
            {
                return SetPasswordResult.Disabled;
            }

            if (command.Link is { } token && account.State != PasswordLinks.StateNeededBy(token.Kind))
            {
                return SetPasswordResult.LinkInvalid;
            }

            if (command.MustDiffer
                && account.PasswordHash is { } current
                && PasswordHasher.Verify(current, password).Matches)
            {
                throw Refused(PasswordRules.SameAsCurrent);
            }

            if (command.Link is { } link && !await links.ConsumeAsync(link, cancellation).ConfigureAwait(false))
            {
                return SetPasswordResult.LinkInvalid;
            }

            var now = clock.GetUtcNow();
            account.SetPassword(
                PasswordHasher.Hash(password),
                command.Temporary ? now.AddHours(options.Value.Auth.TempPasswordHours) : null);

            await links.ReplaceAllAsync(account.Id, cancellation).ConfigureAwait(false);

            db.Events.Add(SignInEvent.Record(
                account.Id, command.EventKind, now, command.Origin.TraceId, command.Origin.IpAddress,
                command.Origin.UserAgent, command.KeepSessionId, command.EventDetail, command.ActorAccountId));

            // Saves the account and the event together with the ended sessions.
            await sessions.EndForAccountCoreAsync(
                account.Id, SessionEndReasons.PasswordReset, command.Origin, cancellation, command.KeepSessionId)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        return SetPasswordResult.Done;
    }

    private static ScsProblemException Refused(params string[] codes) =>
        ScsProblemException.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["password"] = codes });
}
