using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Errors;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;

namespace Scs.Operators.Infrastructure;

/// <summary>
/// The locks of the operators' team (spec 011, research R5). Two things must hold whatever
/// arrives at the same moment: one active «مسؤول عام» always remains (FR-038, ق-56), and a role
/// is not deleted while someone is being given it (FR-020). Each is a count across rows, which
/// no constraint can hold; so whoever is about to change the count first takes a lock that
/// lives as long as their transaction, then counts.
/// </summary>
internal static class SuperAdminGuard
{
    private const string SuperAdmins = "scs:ops:super-admins";
    private const string RoleHolders = "scs:ops:role-holders";

    /// <summary>
    /// Refuses when changing or suspending this account would leave no active «مسؤول عام».
    /// Called inside the transaction of the change, before it.
    /// </summary>
    public static async Task EnsureAnotherRemainsAsync(OperatorsDbContext db, Guid exceptOperatorId, CancellationToken cancellation)
    {
        await LockAsync(db, SuperAdmins, cancellation).ConfigureAwait(false);

        var superAdmin = OperatorPermissions.SystemRoles.IdOf(OperatorPermissions.SystemRoles.SuperAdmin);
        var another = await db.Operators.AsNoTracking()
            .AnyAsync(
                o => o.Id != exceptOperatorId && o.RoleId == superAdmin && o.State == OperatorStates.Active,
                cancellation)
            .ConfigureAwait(false);

        if (!another)
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsLastSuperAdmin);
        }
    }

    /// <summary>Held by whoever gives a role to an account and by whoever deletes a role.</summary>
    public static Task LockRoleHoldersAsync(OperatorsDbContext db, CancellationToken cancellation) =>
        LockAsync(db, RoleHolders, cancellation);

    private static async Task LockAsync(OperatorsDbContext db, string resource, CancellationToken cancellation)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The lock lives as long as a transaction: open one first.");
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DECLARE @held int;
             EXEC @held = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
             IF @held < 0 THROW 50001, 'The operators'' team is busy with another change.', 1;
             """,
            cancellation).ConfigureAwait(false);
    }
}
