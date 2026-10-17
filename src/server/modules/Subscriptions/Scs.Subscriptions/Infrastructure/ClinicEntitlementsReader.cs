using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Plans;
using Scs.BuildingBlocks.Tenancy;
using Scs.Plans.Contracts;
using Scs.Subscriptions.Contracts;
using Scs.Subscriptions.Domain;
using Scs.Subscriptions.Infrastructure.Persistence;
using Scs.Tenancy.Contracts;

namespace Scs.Subscriptions.Infrastructure;

/// <summary>
/// The one answer to "what does this clinic have" (spec 014, FR-002, research R3): the link, the
/// plan (013), the whole catalogue (012) and the clinic's specialty, worked out by the rules once
/// per request and never kept across requests, so an operator's save applies to the very next
/// check (FR-007). It also answers the shared gate and the limit helper.
/// </summary>
internal sealed class ClinicEntitlementsReader(
    SubscriptionsDbContext db,
    IClinicContext clinic,
    IPlanBook plans,
    IFeatureCatalogue catalogue,
    IClinicProfile profile) : IClinicEntitlements, IPlanAnswers
{
    private (Guid Clinic, ClinicEntitlements Answer)? _memo;

    public async Task<ClinicEntitlements> CurrentAsync(CancellationToken cancellation)
    {
        var clinicId = clinic.Require();
        if (_memo is { } held && held.Clinic == clinicId)
        {
            return held.Answer;
        }

        var planId = await db.ClinicPlans.AsNoTracking()
            .Where(p => p.ClinicId == clinicId)
            .Select(p => p.PlanId)
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        // A plan that no longer resolves reads as no plan: the baseline, never a failure.
        var plan = planId is { } id ? await plans.FindAsync(id, cancellation).ConfigureAwait(false) : null;
        var features = await catalogue.AllAsync(cancellation).ConfigureAwait(false);
        var own = await profile.GetOwnAsync(cancellation).ConfigureAwait(false);

        var worked = Entitlements.Work(
            plan is null ? null : new PlanGrid(plan.Id, plan.NameAr, plan.NameEn, [.. plan.Features.Select(f => new PlanRow(f.Key, f.Limit))]),
            [
                .. features.Select(f => new CatalogueRow(
                    f.Key, f.Kind, f.NameAr, f.NameEn, f.UnitAr, f.UnitEn, f.Baseline, f.Specialty,
                    Built: f.Status == "built", f.Monthly)),
            ],
            own.Specialty);

        var answer = new ClinicEntitlements(
            plan?.Id,
            plan?.NameAr,
            plan?.NameEn,
            worked.Values.ToDictionary(
                e => e.Key,
                e => new Entitlement(e.Key, e.Kind, e.Included, e.Limit, e.Enforced, e.Monthly, e.NameAr, e.NameEn, e.UnitAr, e.UnitEn),
                StringComparer.Ordinal));

        _memo = (clinicId, answer);
        return answer;
    }

    /// <summary>The clinic's plan was just set in this request: the next question reads it again.</summary>
    public void Forget() => _memo = null;

    public async Task EnsureIncludedAsync(string featureKey, CancellationToken cancellation)
    {
        var feature = (await CurrentAsync(cancellation).ConfigureAwait(false)).Of(featureKey);
        if (feature is { Enforced: true, Included: false })
        {
            throw PlanRefusals.NotIncluded(featureKey);
        }
    }

    public async Task<int?> LimitOfAsync(string featureKey, CancellationToken cancellation)
    {
        var feature = (await CurrentAsync(cancellation).ConfigureAwait(false)).Of(featureKey);
        return feature.Enforced ? feature.Limit : null;
    }

    public async Task<bool> IsWithheldAsync(IReadOnlyList<string> featureKeys, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(featureKeys);
        var answer = await CurrentAsync(cancellation).ConfigureAwait(false);
        return featureKeys.All(key => answer.Of(key) is { Enforced: true, Included: false });
    }

    public async Task<TitleValue> NameOfAsync(string featureKey, CancellationToken cancellation)
    {
        var feature = (await CurrentAsync(cancellation).ConfigureAwait(false)).Of(featureKey);
        return new TitleValue(feature.NameAr, feature.NameEn);
    }
}
