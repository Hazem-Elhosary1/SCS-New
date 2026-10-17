using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Errors;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Plans;

/// <summary>
/// Refusing at a counted limit (spec 014, SH-017, ق-٤٠, research R7): the module that owns the
/// records counts them, on its own connection, inside the transaction that writes, under a lock
/// held by that transaction — so two requests for the last unit queue rather than both pass
/// (ق-أ-١٩). A limit is never applied backwards (ق-٤١): what is there is counted, and only the
/// next raise is refused.
/// </summary>
public static class PlanLimits
{
    private const int BusyError = 51401;

    /// <summary>
    /// Returns when <paramref name="adding"/> more fit under the clinic's limit, still holding the
    /// lock, so the caller writes before anyone else counts; refuses with
    /// <c>PLAN_LIMIT_REACHED</c> otherwise. No limit, or a feature nothing enforces yet, returns
    /// at once.
    /// </summary>
    /// <exception cref="InvalidOperationException">No transaction is open on <paramref name="db"/>.</exception>
    public static async Task EnsureRoomAsync(
        this DbContext db,
        IPlanAnswers plan,
        Guid clinicId,
        string featureKey,
        Func<CancellationToken, Task<int>> count,
        int adding,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(count);

        var limit = await plan.LimitOfAsync(featureKey, cancellation).ConfigureAwait(false);
        if (limit is null)
        {
            return;
        }

        await db.LockAsync($"scs:plan-limit:{clinicId:N}:{featureKey}", cancellation).ConfigureAwait(false);
        var used = await count(cancellation).ConfigureAwait(false);
        if (used + adding > limit)
        {
            throw PlanRefusals.LimitReached(
                featureKey, limit.Value, await plan.NameOfAsync(featureKey, cancellation).ConfigureAwait(false));
        }
    }

    /// <summary>
    /// Takes the named lock for the rest of the open transaction. Waits up to ten seconds; then
    /// refuses with <c>LIMIT_BUSY</c>.
    /// </summary>
    public static async Task LockAsync(this DbContext db, string resource, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A limit is checked inside the transaction that writes.");
        }

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 DECLARE @held int;
                 EXEC @held = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                 IF @held < 0 THROW 51401, 'The count is busy with another change.', 1;
                 """,
                cancellation).ConfigureAwait(false);
        }
        catch (SqlException busy) when (busy.Number == BusyError)
        {
            throw new ScsProblemException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.LimitBusy, busy.Message);
        }
    }
}
