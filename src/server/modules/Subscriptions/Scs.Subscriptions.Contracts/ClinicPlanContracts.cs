namespace Scs.Subscriptions.Contracts;

/// <summary>
/// Which plan the established clinic is on (spec 014, FR-001). Set by clinic creation (015) and
/// by a plan change (020); nothing else sets it but the local development route.
/// </summary>
public interface IClinicPlans
{
    /// <summary>The clinic's plan, or null for none.</summary>
    Task<Guid?> CurrentAsync(CancellationToken cancellation);

    /// <summary>
    /// Puts the established clinic on the plan, or on none, and records
    /// <c>subscription.plan-set</c> in the clinic's trail and the platform's, in one transaction
    /// (FR-030). An unknown plan is refused with <c>NOT_FOUND</c>; the same plan again changes and
    /// records nothing.
    /// </summary>
    Task SetAsync(Guid? planId, CancellationToken cancellation);
}
