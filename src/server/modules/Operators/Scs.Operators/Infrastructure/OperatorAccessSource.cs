using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Security;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure.Persistence;
using Scs.SharedKernel.Access;

namespace Scs.Operators.Infrastructure;

/// <summary>
/// What an operator may do, read from their role as it is now (spec 011, FR-005, ق-53). One
/// read, by key; asked once per request and never remembered between requests, which is what
/// lets a change to a role reach its holders at their next request without ending a session.
/// </summary>
internal sealed class OperatorAccessSource(OperatorsDbContext db) : IOperatorAccessSource
{
    public async Task<OperatorGrant> LoadAsync(Guid operatorId, CancellationToken cancellation)
    {
        var role = await (
                from account in db.Operators.AsNoTracking()
                join held in db.Roles.AsNoTracking() on account.RoleId equals held.Id
                where account.Id == operatorId && account.State == OperatorStates.Active
                select new { held.Id, held.Name, held.SystemKey })
            .SingleOrDefaultAsync(cancellation)
            .ConfigureAwait(false);

        if (role is null)
        {
            return OperatorGrant.Nothing;
        }

        var codes = await db.RolePermissions.AsNoTracking()
            .Where(permission => permission.RoleId == role.Id)
            .Select(permission => permission.Code)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        // A code that has left the catalogue since it was stored opens nothing.
        return new OperatorGrant(
            role.Id,
            role.Name,
            role.SystemKey,
            codes.Where(OperatorPermissions.IsKnown).ToHashSet(StringComparer.Ordinal));
    }
}
