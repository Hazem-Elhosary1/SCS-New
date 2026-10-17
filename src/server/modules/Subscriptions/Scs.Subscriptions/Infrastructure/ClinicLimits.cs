using Scs.BuildingBlocks.Tenancy;
using Scs.SharedKernel.Plans;
using Scs.Subscriptions.Contracts;
using Scs.Subscriptions.Domain;

namespace Scs.Subscriptions.Infrastructure;

/// <summary>
/// The eight counted limits in one read (spec 014, FR-024, SH-016): the catalogue's names, the
/// clinic's numbers, and what is used — counted by the module that owns the records, or by the
/// meter. A limit nothing counts yet says so, and is never shown as 0.
/// </summary>
internal sealed class ClinicLimits(
    IClinicContext clinic,
    ClinicEntitlementsReader entitlements,
    MonthlyMeter meter,
    IEnumerable<ILimitCounter> counters,
    TimeProvider clock) : IClinicLimits
{
    public async Task<IReadOnlyList<LimitReading>> ReadAsync(CancellationToken cancellation)
    {
        var clinicId = clinic.Require();
        var answer = await entitlements.CurrentAsync(cancellation).ConfigureAwait(false);
        var counting = counters.ToDictionary(c => c.FeatureKey, StringComparer.Ordinal);
        var now = clock.GetUtcNow();
        var month = CairoMonth.Of(now);
        var renews = CairoMonth.RenewsOn(now);

        var readings = new List<LimitReading>(FeatureKeys.CountedLimits.Count);
        foreach (var key in FeatureKeys.CountedLimits)
        {
            var feature = answer.Of(key);
            var monthly = FeatureKeys.IsMonthly(key);
            int? used = monthly
                ? await meter.UsedAsync(clinicId, key, month, cancellation).ConfigureAwait(false)
                : counting.TryGetValue(key, out var counter)
                    ? await counter.CountAsync(cancellation).ConfigureAwait(false)
                    : null;

            readings.Add(new LimitReading(
                key, feature.NameAr, feature.NameEn, feature.UnitAr, feature.UnitEn, feature.Limit, used, monthly,
                monthly ? renews : null));
        }

        return readings;
    }
}
