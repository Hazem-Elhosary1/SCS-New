namespace Scs.Plans.Contracts;

/// <summary>A feature a plan includes: its key, its kind and, for a limit, its number (null for no limit).</summary>
/// <param name="Kind"><c>switch</c> or <c>limit</c>.</param>
/// <param name="Limit">1–1,000,000 for a limit; null for no limit, and always null for a switch.</param>
public sealed record PlanIncluded(string Key, string Kind, int? Limit);

/// <summary>
/// One plan as another module reads it (spec 013, FR-025): what it is, what it costs and what it
/// includes. A feature not in <see cref="Features"/> is not included; what a clinic then has is the
/// feature's baseline (<see cref="IFeatureCatalogue.FindAsync"/>, ق-٣٨).
/// </summary>
/// <param name="State"><c>active</c> or <c>disabled</c>.</param>
public sealed record PlanEntitlements(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    string State,
    decimal MonthlyEgp,
    decimal AnnualEgp,
    decimal MonthlyUsd,
    decimal AnnualUsd,
    IReadOnlyList<PlanIncluded> Features);

/// <summary>A plan a clinic may be given or moved to (an active one).</summary>
public sealed record AssignablePlan(Guid Id, string Code, string NameAr, string NameEn, decimal MonthlyEgp, decimal AnnualEgp);

/// <summary>The plans' one read for other modules: the clinic (015), its subscription (016) and enforcement (014). In-process only.</summary>
public interface IPlanBook
{
    /// <summary>The plan of this id, active or disabled; null for an id that is no plan.</summary>
    Task<PlanEntitlements?> FindAsync(Guid id, CancellationToken cancellation);

    /// <summary>The active plans — a disabled one is never given to a clinic (ق-٤٧) — by monthly price, then by English name.</summary>
    Task<IReadOnlyList<AssignablePlan>> AssignableAsync(CancellationToken cancellation);
}

/// <summary>A plan's clinics, counted.</summary>
/// <param name="Clinics">Clinics on the plan whose stored state is active (§3-5).</param>
/// <param name="NewThisMonth">Of those, the ones that joined it this month (Cairo time).</param>
/// <param name="OnTrial">Clinics on the plan in their trial.</param>
/// <param name="MonthlyRevenue">The plan's monthly revenue: OP-A-189's rule.</param>
public sealed record PlanSubscriberCounts(int Clinics, int NewThisMonth, int OnTrial, decimal MonthlyRevenue);

/// <summary>The platform's monthly plan revenue now and at the end of last month (null when there was no last month).</summary>
public sealed record PlanRevenue(decimal Now, decimal? LastMonthEnd);

/// <summary>A clinic on a plan, with the line the plan's detail shows for it («نشطة · تتجدد <التاريخ>» …).</summary>
public sealed record PlanClinic(Guid ClinicId, string NameAr, string NameEn, string StateAr, string StateEn, DateTimeOffset JoinedAtUtc);

/// <summary>
/// The clinics on plans, as the plans' screens count them (spec 013, research R7). Clinics are
/// given plans by spec 015 and subscribe by 016: until then the plans module answers "no clinics",
/// and the module that gives clinics their plans registers its own, which replaces that answer.
/// The plans module never reads a clinic itself: the clinic depends on the plan, never the reverse.
/// </summary>
public interface IPlanSubscribers
{
    /// <summary>Each plan's clinics; a plan with none may be left out of the answer.</summary>
    Task<IReadOnlyDictionary<Guid, PlanSubscriberCounts>> CountsAsync(IReadOnlyCollection<Guid> planIds, CancellationToken cancellation);

    /// <summary>The platform's monthly plan revenue (OP-A-189); null while no clinic is on a plan.</summary>
    Task<PlanRevenue?> RevenueAsync(CancellationToken cancellation);

    /// <summary>The clinics on one plan, the newest to join first (§7-10).</summary>
    Task<IReadOnlyList<PlanClinic>> ClinicsAsync(Guid planId, CancellationToken cancellation);
}
