using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Identity.Infrastructure.Persistence;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// Every clinic's owner, read across clinics for the operators' list (spec 015, FR-009; the
/// declared crossing <c>read-clinic-owners-as-operator</c>): the name, email and mobile of each
/// clinic's earliest account holding the owner role, and nothing else — never a secret.
/// </summary>
internal sealed class OperatorClinicOwners(ICrossClinicConnectionFactory crossing)
{
    public async Task<IReadOnlyList<OwnerContact>> ListAsync(CancellationToken cancellation)
    {
        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        crossing.Configure(builder);
        var db = new IdentityDbContext(builder.Options, new ClinicContext());
        await using (db.ConfigureAwait(false))
        {
            // No clinic is established here: the application's own filter would match nothing.
            var ownerRoles = OwnerRole.Ids(db.Roles.AsNoTracking().IgnoreQueryFilters());
            var owners = await (
                    from account in db.Accounts.AsNoTracking().IgnoreQueryFilters()
                    where account.RoleId != null && ownerRoles.Contains(account.RoleId.Value)
                    orderby account.CreatedAtUtc
                    select new { account.ClinicId, account.Email, account.FullName, account.Mobile })
                .ToListAsync(cancellation).ConfigureAwait(false);

            return [.. owners.GroupBy(o => o.ClinicId).Select(g => g.First()).Select(o => new OwnerContact(o.ClinicId, o.Email, o.FullName, o.Mobile))];
        }
    }
}
