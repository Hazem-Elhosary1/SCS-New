using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Plans;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel.Plans;
using Scs.Subscriptions.Contracts;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// The plan's «المستخدمون» (spec 014, FR-015, FR-016; research R8). An account takes a seat when
/// it is active, or invited with an invitation that has not expired: a live link, or none yet
/// within an invitation's life from its creation — written a moment ago, or whose email did not
/// leave. Disabled accounts and lapsed invitations take none (ق-٠٦, ق-١٠٣). The check runs
/// wherever an account that takes no seat is about to take one, inside the transaction that
/// writes it, so it holds whoever asks — the clinic, or an operator (SH-017).
/// </summary>
internal sealed class Seats(
    IdentityDbContext db, IClinicContext clinic, IPlanAnswers plan, TimeProvider clock, IOptions<ScsOptions> options)
    : ILimitCounter
{
    public string FeatureKey => FeatureKeys.MaxUsers;

    public Task<int> CountAsync(CancellationToken cancellation) => Counted().CountAsync(cancellation);

    /// <summary>Whether the account already takes a seat.</summary>
    public Task<bool> TakesASeatAsync(Guid accountId, CancellationToken cancellation) =>
        Counted().AnyAsync(a => a.Id == accountId, cancellation);

    /// <summary>
    /// Refuses with <c>PLAN_LIMIT_REACHED</c> when one more account would pass the plan's limit.
    /// An account that already takes a seat passes; so does every account under no limit.
    /// </summary>
    /// <param name="accountId">The account about to take a seat; null for one about to be created.</param>
    public async Task EnsureSeatAsync(Guid? accountId, CancellationToken cancellation)
    {
        if (accountId is { } id && await TakesASeatAsync(id, cancellation).ConfigureAwait(false))
        {
            return;
        }

        await db.EnsureRoomAsync(plan, clinic.Require(), FeatureKey, CountAsync, adding: 1, cancellation).ConfigureAwait(false);
    }

    private IQueryable<StaffAccount> Counted()
    {
        var now = clock.GetUtcNow();
        var invitedSince = now - TimeSpan.FromHours(options.Value.Auth.InviteLinkHours);
        var invitations = db.PasswordLinks.Where(l => l.Kind == PasswordLinkKinds.NewAccount);

        return db.Accounts.Where(a =>
            a.State == AccountStates.Active
            || (a.State == AccountStates.Invited
                && (invitations.Any(l => l.AccountId == a.Id && l.UsedAtUtc == null && l.ReplacedAtUtc == null && l.ExpiresAtUtc > now)
                    || (!invitations.Any(l => l.AccountId == a.Id) && a.CreatedAtUtc > invitedSince))));
    }
}
