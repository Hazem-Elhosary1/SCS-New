using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Plans;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.SharedKernel;
using Scs.SharedKernel.Plans;
using Scs.Subscriptions.Contracts;
using Scs.Subscriptions.Domain;
using Scs.Subscriptions.Infrastructure.Persistence;

namespace Scs.Subscriptions.Infrastructure;

/// <summary>
/// The meter of the four monthly limits (spec 014, SH-018, research R10): a ledger per clinic,
/// limit and month in Cairo. A use takes the lock of the limit, sums the month and either writes
/// or refuses; nothing is reset at midnight, because the month is worked out on every write.
/// </summary>
internal sealed class MonthlyMeter(
    SubscriptionsDbContext db,
    IClinicContext clinic,
    ClinicEntitlementsReader entitlements,
    IHttpContextAccessor http,
    TimeProvider clock) : IMonthlyMeter
{
    public async Task<Guid> UseAsync(MeterUse use, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(use);
        RequireMonthly(use.FeatureKey);
        if (use.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(use), "A use takes at least one unit.");
        }

        var clinicId = clinic.Require();
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await db.LockAsync(Resource(clinicId, use.FeatureKey), cancellation).ConfigureAwait(false);

            var now = clock.GetUtcNow();
            var month = CairoMonth.Of(now);
            var allowance = await entitlements.LimitOfAsync(use.FeatureKey, cancellation).ConfigureAwait(false);
            var used = await UsedAsync(clinicId, use.FeatureKey, month, cancellation).ConfigureAwait(false);
            var beyond = allowance is { } limit && used + use.Amount > limit;

            if (beyond && !use.CompletesPromise)
            {
                throw PlanRefusals.LimitReached(
                    use.FeatureKey, allowance!.Value, await entitlements.NameOfAsync(use.FeatureKey, cancellation).ConfigureAwait(false));
            }

            var entry = MeterEntry.Use(use.FeatureKey, month, use.Amount, now, use.Purpose, use.Reference, By(), beyond);
            db.Meter.Add(entry);
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
            return entry.Id;
        }
    }

    public async Task GiveBackAsync(Guid useId, int amount, CancellationToken cancellation)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A give-back returns at least one unit.");
        }

        var clinicId = clinic.Require();
        var use = await db.Meter.AsNoTracking()
                      .SingleOrDefaultAsync(e => e.Id == useId && e.Amount > 0, cancellation)
                      .ConfigureAwait(false)
                  ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "No such use.");

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await db.LockAsync(Resource(clinicId, use.FeatureKey), cancellation).ConfigureAwait(false);

            var givenBack = await db.Meter.AsNoTracking()
                .Where(e => e.GivesBack == useId)
                .SumAsync(e => (int?)e.Amount, cancellation)
                .ConfigureAwait(false) ?? 0;

            // Give-backs are negative: what is left of the use is its amount plus them.
            if (amount > use.Amount + givenBack)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.MeterGiveBackTooMuch);
            }

            db.Meter.Add(MeterEntry.GiveBack(use, amount, clock.GetUtcNow(), By()));
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    public async Task<MeterReading> ReadAsync(string featureKey, string? month, CancellationToken cancellation)
    {
        RequireMonthly(featureKey);
        if (month is not null && !CairoMonth.IsMonth(month))
        {
            throw new ArgumentOutOfRangeException(nameof(month), "A month is yyyy-MM.");
        }

        var clinicId = clinic.Require();
        var now = clock.GetUtcNow();
        var which = month ?? CairoMonth.Of(now);
        var allowance = (await entitlements.CurrentAsync(cancellation).ConfigureAwait(false)).Of(featureKey).Limit;
        var used = await UsedAsync(clinicId, featureKey, which, cancellation).ConfigureAwait(false);
        var first = DateOnly.ParseExact(which + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new MeterReading(
            featureKey,
            which,
            allowance,
            used,
            Left: allowance is { } a ? Math.Max(0, a - used) : 0,
            Overrun: allowance is { } b ? Math.Max(0, used - b) : 0,
            RenewsOn: first.AddMonths(1));
    }

    /// <summary>What the clinic used of a limit in a month, give-backs taken off.</summary>
    internal Task<int> UsedAsync(Guid clinicId, string featureKey, string month, CancellationToken cancellation) =>
        db.Meter.AsNoTracking()
            .Where(e => e.ClinicId == clinicId && e.FeatureKey == featureKey && e.Month == month)
            .SumAsync(e => e.Amount, cancellation);

    private static void RequireMonthly(string featureKey)
    {
        if (!FeatureKeys.IsMonthly(featureKey))
        {
            throw new ArgumentOutOfRangeException(nameof(featureKey), $"{featureKey} is not one of the four monthly limits.");
        }
    }

    private static string Resource(Guid clinicId, string featureKey) => $"scs:meter:{clinicId:N}:{featureKey}";

    /// <summary>Who takes from the meter, as an id and never as personal text.</summary>
    private string By()
    {
        var user = http.HttpContext?.User;
        if (user?.FindFirstValue(StaffClaims.AccountId) is { } account)
        {
            return $"account:{account}";
        }

        if (user?.FindFirstValue(OperatorClaims.OperatorId) is { } op)
        {
            return $"operator:{op}";
        }

        return $"job:{clinic.Source ?? "platform"}";
    }
}
