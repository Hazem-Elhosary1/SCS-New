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

    /// <summary>
    /// Puts a new clinic on its plan with the end of its trial (spec 015, FR-004), recording
    /// <c>subscription.plan-set</c> in the clinic's trail and the platform's, in one transaction. A
    /// plan that is not offered to clinics (disabled, or unknown) is refused with <c>PLAN_DISABLED</c>.
    /// </summary>
    Task StartAsync(Guid planId, DateTimeOffset trialEndsAtUtc, CancellationToken cancellation);

    /// <summary>The established clinic's plan, when it was set and when its trial ends; null when it has none.</summary>
    Task<ClinicPlanState?> StateAsync(CancellationToken cancellation);

    /// <summary>
    /// Takes away the plan an add started before it failed, so that nothing of the add is left on
    /// the given-up clinic (spec 015, SC-003). Only an add's undo calls it; its trail entry stays.
    /// </summary>
    Task UndoStartAsync(CancellationToken cancellation);
}

/// <summary>A clinic's plan link (spec 015).</summary>
public sealed record ClinicPlanState(Guid? PlanId, DateTimeOffset SetAtUtc, DateTimeOffset? TrialEndsAtUtc);
