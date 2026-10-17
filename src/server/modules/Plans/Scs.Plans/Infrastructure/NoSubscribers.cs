using Scs.Plans.Contracts;

namespace Scs.Plans.Infrastructure;

/// <summary>
/// "No clinic is on any plan" — the answer until spec 015 gives clinics their plans and registers
/// its own <see cref="IPlanSubscribers"/>, which replaces this one (research R7).
/// </summary>
internal sealed class NoSubscribers : IPlanSubscribers
{
    private static readonly IReadOnlyDictionary<Guid, PlanSubscriberCounts> None = new Dictionary<Guid, PlanSubscriberCounts>();

    public Task<IReadOnlyDictionary<Guid, PlanSubscriberCounts>> CountsAsync(IReadOnlyCollection<Guid> planIds, CancellationToken cancellation) =>
        Task.FromResult(None);

    public Task<PlanRevenue?> RevenueAsync(CancellationToken cancellation) => Task.FromResult<PlanRevenue?>(null);

    public Task<IReadOnlyList<PlanClinic>> ClinicsAsync(Guid planId, CancellationToken cancellation) =>
        Task.FromResult<IReadOnlyList<PlanClinic>>([]);
}
