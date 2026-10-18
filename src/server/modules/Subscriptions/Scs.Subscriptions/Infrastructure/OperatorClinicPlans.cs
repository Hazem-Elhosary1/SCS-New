using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Tenancy;
using Scs.Subscriptions.Infrastructure.Persistence;

namespace Scs.Subscriptions.Infrastructure;

/// <summary>A clinic's plan link, as the operators' list and the plans' figures read it.</summary>
internal sealed record ClinicPlanRow(Guid ClinicId, Guid? PlanId, DateTimeOffset SetAtUtc, DateTimeOffset? TrialEndsAtUtc);

/// <summary>
/// Every clinic's plan link, read across clinics (spec 015, FR-007 – FR-010, FR-029; the declared
/// crossing <c>read-clinic-plans-as-operator</c>): the plan, when it was set and when the trial
/// ends, and nothing else.
/// </summary>
internal sealed class OperatorClinicPlans(ICrossClinicConnectionFactory crossing)
{
    public async Task<IReadOnlyList<ClinicPlanRow>> ListAsync(CancellationToken cancellation)
    {
        var builder = new DbContextOptionsBuilder<SubscriptionsDbContext>();
        crossing.Configure(builder);
        var db = new SubscriptionsDbContext(builder.Options, new ClinicContext());
        await using (db.ConfigureAwait(false))
        {
            // No clinic is established here: the application's own filter would match nothing.
            return await db.ClinicPlans.AsNoTracking().IgnoreQueryFilters()
                .Select(p => new ClinicPlanRow(p.ClinicId, p.PlanId, p.SetAtUtc, p.TrialEndsAtUtc))
                .ToListAsync(cancellation).ConfigureAwait(false);
        }
    }
}
