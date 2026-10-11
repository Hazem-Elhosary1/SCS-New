using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;
using Scs.Tenancy.Contracts;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// The three roles every clinic starts with (SH-052, CL-AD-082). One routine creates them, for
/// a clinic that has none: called when a clinic is registered, by the seed, and for spec 015.
/// </summary>
internal sealed class ReadyRoles(IdentityDbContext db, IClinicContext clinic, TimeProvider clock)
{
    public const string OwnerName = "مالك العيادة";
    public const string ManagerName = "مدير العيادة";
    public const string DoctorName = "طبيب";

    // The one-line descriptions of the design's roles list (frame 6675:19376).
    public const string OwnerDescription = "كل صلاحيات العيادة";
    public const string ManagerDescription = "تشغيل العيادة اليومي، دون غرفة الكشف ولا الإعدادات الفنية ولا الاشتراك";
    public const string DoctorDescription = "غرفة الكشف وملفات المرضى ومواعيد الطبيب نفسه";

    /// <summary>
    /// Creates the three when the clinic has no owner role. That role can never be removed, so
    /// its presence means "already done": a clinic that removed «مدير العيادة» does not get it back.
    /// </summary>
    public async Task EnsureAsync(CancellationToken cancellation)
    {
        clinic.Require();

        if (await db.Roles.AnyAsync(r => r.HoldsEverything, cancellation).ConfigureAwait(false))
        {
            return;
        }

        var now = clock.GetUtcNow();
        var manager = Role.Ready(ReadyRoleKeys.Manager, ManagerName, ManagerDescription, now);
        manager.Hold(ReadySets.Manager);
        var doctor = Role.Ready(ReadyRoleKeys.Doctor, DoctorName, DoctorDescription, now);
        doctor.Hold(ReadySets.Doctor);

        db.Roles.AddRange(Role.Ready(ReadyRoleKeys.Owner, OwnerName, OwnerDescription, now), manager, doctor);

        try
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        catch (DbUpdateException failure) when (failure.IsUniqueViolation())
        {
            // Two callers at the same moment: the other one created them.
            db.ChangeTracker.Clear();
        }
    }
}

/// <summary>Gives a clinic its ready roles the moment it is registered.</summary>
internal sealed class RolesProvisioner(ReadyRoles ready) : IClinicProvisioner
{
    public Task ProvisionAsync(CancellationToken cancellation) => ready.EnsureAsync(cancellation);
}

/// <summary>
/// What an account may do right now: its role and that role's codes, in one read. Nothing is
/// kept between requests, so a change to a role is seen by the very next one (spec 005, FR-024).
/// </summary>
internal sealed class StaffAccessSource(IdentityDbContext db) : IStaffAccessSource
{
    public async Task<StaffGrant> LoadAsync(Guid accountId, CancellationToken cancellation)
    {
        var role = await (
            from account in db.Accounts.AsNoTracking()
            join held in db.Roles.AsNoTracking() on account.RoleId equals held.Id
            where account.Id == accountId
            select new
            {
                held.Id,
                held.Name,
                held.ReadyKey,
                held.IsRenamed,
                held.HoldsEverything,
                Codes = held.Permissions.Select(p => p.Code).ToList(),
            }).SingleOrDefaultAsync(cancellation).ConfigureAwait(false);

        if (role is null)
        {
            return StaffGrant.Nothing;
        }

        // A stored code that a later release took out of the catalogue grants nothing.
        var codes = role.HoldsEverything
            ? PermissionCatalogue.All.Select(p => p.Code)
            : role.Codes.Where(PermissionCatalogue.Contains);

        return new StaffGrant(
            role.Id, role.Name, role.IsRenamed ? null : role.ReadyKey, codes.ToHashSet(StringComparer.Ordinal));
    }
}

/// <summary>
/// Storing roles and giving them to accounts, for the established clinic (spec 005, FR-012 and
/// FR-013). Every change runs in one transaction under the clinic's "roles" lock, so "a role in
/// use" and "the last owner" cannot be raced.
/// </summary>
internal sealed class ClinicRoles(
    IdentityDbContext db,
    IClinicContext clinic,
    ReadyRoles ready,
    SessionService sessions,
    ICurrentStaff staff,
    TimeProvider clock) : IClinicRoles
{
    private static readonly string[] ReadyOrder = [ReadyRoleKeys.Owner, ReadyRoleKeys.Manager, ReadyRoleKeys.Doctor];

    public Task ProvisionAsync(CancellationToken cancellation) => ready.EnsureAsync(cancellation);

    public async Task<IReadOnlyList<RoleSummary>> ListAsync(CancellationToken cancellation)
    {
        clinic.Require();

        var roles = await db.Roles.AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.ReadyKey,
                r.IsRenamed,
                r.Description,
                r.Category,
                r.HoldsEverything,
                Count = r.Permissions.Count,
            })
            .ToListAsync(cancellation).ConfigureAwait(false);

        return
        [
            .. roles
                .OrderBy(r => r.ReadyKey is null ? ReadyOrder.Length : Array.IndexOf(ReadyOrder, r.ReadyKey))
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .Select(r => new RoleSummary(
                    r.Id,
                    r.Name,
                    r.IsRenamed ? null : r.ReadyKey,
                    r.Description,
                    r.Category,
                    r.HoldsEverything,
                    r.HoldsEverything ? PermissionCatalogue.Count : r.Count)),
        ];
    }

    public async Task<Guid> CreateAsync(
        string name, string? description, IReadOnlyCollection<string> permissions, CancellationToken cancellation)
    {
        clinic.Require();
        var (cleanName, cleanDescription) = Validate(name, description, permissions);

        return await InLockAsync(
            async () =>
            {
                var role = Role.Custom(cleanName, cleanDescription, Actor(), clock.GetUtcNow());
                role.Hold(permissions);
                db.Roles.Add(role);
                await SaveNamedAsync(cancellation).ConfigureAwait(false);
                return role.Id;
            },
            cancellation).ConfigureAwait(false);
    }

    public async Task<RoleChange> UpdateAsync(
        Guid roleId, string name, string? description, IReadOnlyCollection<string> permissions, CancellationToken cancellation)
    {
        clinic.Require();

        return await InLockAsync(
            async () =>
            {
                var role = await FindAsync(roleId, cancellation).ConfigureAwait(false);
                if (role.HoldsEverything)
                {
                    throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.RoleFixed);
                }

                var (cleanName, cleanDescription) = Validate(name, description, permissions);
                role.Describe(cleanName, cleanDescription, Actor(), clock.GetUtcNow());
                var (added, removed) = role.Hold(permissions);
                await SaveNamedAsync(cancellation).ConfigureAwait(false);
                return new RoleChange(role.Id, PermissionCatalogue.InOrder(added), PermissionCatalogue.InOrder(removed));
            },
            cancellation).ConfigureAwait(false);
    }

    public async Task RemoveAsync(Guid roleId, CancellationToken cancellation)
    {
        clinic.Require();

        await InLockAsync(
            async () =>
            {
                var role = await FindAsync(roleId, cancellation).ConfigureAwait(false);
                if (role.HoldsEverything)
                {
                    throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.RoleFixed);
                }

                var holders = await db.Accounts.CountAsync(a => a.RoleId == roleId, cancellation).ConfigureAwait(false);
                if (holders > 0)
                {
                    throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.RoleInUse)
                    {
                        Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["holders"] = holders },
                    };
                }

                db.Roles.Remove(role);
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
                return true;
            },
            cancellation).ConfigureAwait(false);
    }

    public async Task AssignAsync(Guid accountId, Guid roleId, CancellationToken cancellation)
    {
        clinic.Require();

        if (staff.IsSignedIn && staff.AccountId == accountId)
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OwnRole);
        }

        await InLockAsync(
            async () =>
            {
                var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellation).ConfigureAwait(false)
                              ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
                var role = await FindAsync(roleId, cancellation).ConfigureAwait(false);

                if (account.RoleId == role.Id)
                {
                    return true;
                }

                if (account.RoleId is { } current)
                {
                    var holdsOwner = await db.Roles.AnyAsync(r => r.Id == current && r.HoldsEverything, cancellation)
                        .ConfigureAwait(false);
                    var otherOwners = holdsOwner
                                      && await db.Accounts.AnyAsync(a => a.RoleId == current && a.Id != accountId, cancellation)
                                          .ConfigureAwait(false);
                    if (holdsOwner && !otherOwners)
                    {
                        throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.LastOwner);
                    }
                }

                var hadRole = account.RoleId is not null;
                account.AssignRole(role.Id);
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

                if (hadRole)
                {
                    // A different role ends the account's sessions (SH-045). A first role ends
                    // nothing: until now the account could do nothing.
                    await sessions.EndForAccountCoreAsync(accountId, SessionEndings.RoleChanged, RequestOrigin.None, cancellation)
                        .ConfigureAwait(false);
                }

                return true;
            },
            cancellation).ConfigureAwait(false);
    }

    private Guid? Actor() => staff.IsSignedIn ? staff.AccountId : null;

    private async Task<Role> FindAsync(Guid roleId, CancellationToken cancellation) =>
        await db.Roles.Include(r => r.Permissions).SingleOrDefaultAsync(r => r.Id == roleId, cancellation).ConfigureAwait(false)
        ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    private static (string Name, string? Description) Validate(
        string? name, string? description, IReadOnlyCollection<string>? permissions)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var cleanName = name?.Trim() ?? "";
        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (cleanName.Length == 0)
        {
            errors["name"] = ["REQUIRED"];
        }
        else if (cleanName.Length > Role.NameMaxLength)
        {
            errors["name"] = ["TOO_LONG"];
        }

        if (cleanDescription?.Length > Role.DescriptionMaxLength)
        {
            errors["description"] = ["TOO_LONG"];
        }

        var problems = PermissionSets.Check(permissions);
        if (!problems.None)
        {
            errors["permissions"] = [.. problems.Codes];
        }

        if (errors.Count == 0)
        {
            return (cleanName, cleanDescription);
        }

        // The refusal names what is wrong, so a screen can point at it: the pairs that break
        // the requirement rule, the codes that are not in the catalogue, the codes given twice.
        var named = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (problems.Missing.Count > 0)
        {
            named["missing"] = problems.Missing.Select(m => new { permission = m.Permission, requires = m.Requires }).ToArray();
        }

        if (problems.Unknown.Count > 0)
        {
            named["unknown"] = problems.Unknown.ToArray();
        }

        if (problems.Repeated.Count > 0)
        {
            named["duplicate"] = problems.Repeated.ToArray();
        }

        throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed)
        {
            Errors = errors,
            Extensions = named.Count == 0 ? null : named,
        };
    }

    private async Task SaveNamedAsync(CancellationToken cancellation)
    {
        try
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        catch (DbUpdateException failure) when (failure.IsUniqueViolation())
        {
            throw ScsProblemException.Validation(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["name"] = ["TAKEN"] });
        }
    }

    /// <summary>One change to the clinic's roles at a time, in a transaction of its own unless the caller has one.</summary>
    private async Task<T> InLockAsync<T>(Func<Task<T>> work, CancellationToken cancellation)
    {
        var own = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false)
            : null;

        try
        {
            var resource = $"scs:roles:{clinic.Require():N}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 DECLARE @held int;
                 EXEC @held = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
                 IF @held < 0 THROW 50001, 'The clinic''s roles are busy with another change.', 1;
                 """,
                cancellation).ConfigureAwait(false);

            var result = await work().ConfigureAwait(false);

            if (own is not null)
            {
                await own.CommitAsync(cancellation).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            if (own is not null)
            {
                await own.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
