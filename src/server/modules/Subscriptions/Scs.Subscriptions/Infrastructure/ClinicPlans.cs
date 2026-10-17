using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Plans;
using Scs.BuildingBlocks.Tenancy;
using Scs.Plans.Contracts;
using Scs.SharedKernel;
using Scs.SharedKernel.Audit;
using Scs.Subscriptions.Contracts;
using Scs.Subscriptions.Domain;
using Scs.Subscriptions.Infrastructure.Persistence;

namespace Scs.Subscriptions.Infrastructure;

/// <summary>
/// Puts the established clinic on a plan (spec 014, FR-001, FR-030): one row, and the change in
/// the clinic's trail and the platform's, in one transaction with it (constitution IV).
/// </summary>
internal sealed class ClinicPlans(
    SubscriptionsDbContext db,
    IClinicContext clinic,
    IPlanBook plans,
    ClinicEntitlementsReader entitlements,
    TimeProvider clock) : IClinicPlans
{
    public const string PlanField = "plan";

    public async Task<Guid?> CurrentAsync(CancellationToken cancellation)
    {
        var clinicId = clinic.Require();
        return await db.ClinicPlans.AsNoTracking()
            .Where(p => p.ClinicId == clinicId)
            .Select(p => p.PlanId)
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);
    }

    public async Task SetAsync(Guid? planId, CancellationToken cancellation)
    {
        var clinicId = clinic.Require();
        var after = planId is { } id
            ? await plans.FindAsync(id, cancellation).ConfigureAwait(false)
              ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "No such plan.")
            : null;

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // One set of a clinic's plan at a time: a second waits for the first and then applies,
            // so neither the row's key nor its version can clash (data-model.md, T056).
            await db.LockAsync($"scs:clinic-plan:{clinicId:N}", cancellation).ConfigureAwait(false);

            var now = clock.GetUtcNow();
            var row = await db.ClinicPlans.SingleOrDefaultAsync(p => p.ClinicId == clinicId, cancellation).ConfigureAwait(false);
            var beforeId = row?.PlanId;

            if (row is null)
            {
                if (planId is null)
                {
                    return;
                }

                db.ClinicPlans.Add(ClinicPlan.Create(clinicId, planId, now));
            }
            else if (!row.Set(planId, now))
            {
                return;
            }

            var before = beforeId is { } was ? await plans.FindAsync(was, cancellation).ConfigureAwait(false) : null;
            var record = new AuditRecord(
                AuditActions.SubscriptionPlanSet,
                new AuditTarget(AuditTarget.Clinic, clinicId),
                Facts: new Dictionary<string, object?>(StringComparer.Ordinal) { ["plan"] = after?.Code ?? "" },
                Changes: [AuditChange.Of(PlanField, before?.Code ?? beforeId?.ToString(), after?.Code)]);

            db.Record(record);
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(record, cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        entitlements.Forget();
    }
}
