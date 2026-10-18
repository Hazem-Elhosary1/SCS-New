using Scs.BuildingBlocks.Security;
using Scs.Plans.Contracts;
using Scs.Subscriptions.Domain;
using Scs.Tenancy.Contracts;

namespace Scs.Subscriptions.Infrastructure;

/// <summary>
/// The clinics on plans, as 013's plan screens count them (spec 015, FR-029; research R13). It
/// replaces the plans module's "no clinics" answer. Revenue is 0 until spec 018 gives clinics a
/// billing cycle: a clinic in trial or with no cycle counts nothing (OP-A-189, the owner's answer).
/// </summary>
internal sealed class PlanSubscribers(
    IClinicRegistry registry, OperatorClinicPlans clinicPlans, ICurrentOperator current, TimeProvider clock) : IPlanSubscribers
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    public async Task<IReadOnlyDictionary<Guid, PlanSubscriberCounts>> CountsAsync(
        IReadOnlyCollection<Guid> planIds, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(planIds);
        var now = clock.GetUtcNow();
        var monthStart = MonthStartInCairo(now);
        var onPlans = await OnPlansAsync(cancellation).ConfigureAwait(false);

        return onPlans
            .Where(c => planIds.Contains(c.Link.PlanId!.Value))
            .GroupBy(c => c.Link.PlanId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new PlanSubscriberCounts(
                    g.Count(c => c.Clinic.State == ClinicStates.Active),
                    g.Count(c => c.Clinic.State == ClinicStates.Active && c.Link.SetAtUtc >= monthStart),
                    g.Count(c => ClinicStatus.Of(c.Clinic.State, c.Link.TrialEndsAtUtc, now).Code == ClinicStatus.Trial),
                    0m));
    }

    public async Task<PlanRevenue?> RevenueAsync(CancellationToken cancellation)
    {
        var onPlans = await OnPlansAsync(cancellation).ConfigureAwait(false);
        return onPlans.Count == 0 ? null : new PlanRevenue(0m, null);
    }

    public async Task<IReadOnlyList<PlanClinic>> ClinicsAsync(Guid planId, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var onPlans = await OnPlansAsync(cancellation).ConfigureAwait(false);
        return
        [
            .. onPlans
                .Where(c => c.Link.PlanId == planId)
                .OrderByDescending(c => c.Link.SetAtUtc)
                .Select(c =>
                {
                    var status = ClinicStatus.Of(c.Clinic.State, c.Link.TrialEndsAtUtc, now);
                    return new PlanClinic(
                        c.Clinic.Id, c.Clinic.NameAr, c.Clinic.NameEn, status.Ar, status.En, c.Link.SetAtUtc,
                        status.Code == ClinicStatus.Trial ? c.Link.TrialEndsAtUtc : null);
                }),
        ];
    }

    /// <summary>Every visible clinic that is on a plan, with its link.</summary>
    private async Task<List<(ClinicListItem Clinic, ClinicPlanRow Link)>> OnPlansAsync(CancellationToken cancellation)
    {
        var who = Who();
        var clinics = (await registry.ListAsync(who, cancellation).ConfigureAwait(false)).ToDictionary(c => c.Id);
        var links = await clinicPlans.ListAsync(cancellation).ConfigureAwait(false);
        return
        [
            .. links
                .Where(l => l.PlanId is not null && clinics.ContainsKey(l.ClinicId))
                .Select(l => (clinics[l.ClinicId], l)),
        ];
    }

    /// <summary>The operator reading the figures, for the record of the list's crossing; the platform when none.</summary>
    private string Who()
    {
        try
        {
            return current.OperatorId.ToString();
        }
        catch (Scs.BuildingBlocks.Errors.ScsProblemException)
        {
            return "system:plan-figures";
        }
    }

    private static DateTimeOffset MonthStartInCairo(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, Cairo);
        var start = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(start, Cairo.GetUtcOffset(start));
    }
}
