using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;
using Scs.Tenancy.Contracts;

namespace Scs.Identity.Infrastructure;

/// <summary>What a change to a role leaves in the audit trail (spec 007, FR-025).</summary>
internal static class RoleEntries
{
    public const string Name = "name";
    public const string Description = "description";
    public const string Permissions = "permissions";
    public const string RoleField = "role";

    /// <summary>A ready role under its own name keeps its key, so it reads in the reader's language (FR-037).</summary>
    public static string? KeyOf(Role role) => role.IsRenamed ? null : role.ReadyKey;

    public static AuditTarget Target(Role role) => new(AuditTarget.Role, role.Id, role.Name, KeyOf(role));

    /// <param name="bySystem">The platform created it: a ready role, or a seeded one.</param>
    public static AuditRecord Created(Role role, bool bySystem = false)
    {
        // The owner role stores no rows: it holds whatever the catalogue holds.
        var held = role.HoldsEverything
            ? PermissionCatalogue.All.Select(p => p.Code)
            : PermissionCatalogue.InOrder(role.Permissions.Select(p => p.Code));

        return new AuditRecord(
            AuditActions.RoleCreated,
            Target(role),
            Changes:
            [
                new AuditChange(Name, null, role.Name, AfterKey: KeyOf(role)),
                AuditChange.Of(Description, null, role.Description),
                AuditChange.OfSet(Permissions, held, []),
            ])
        {
            BySystem = bySystem,
        };
    }
}

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

        var owner = Role.Ready(ReadyRoleKeys.Owner, OwnerName, OwnerDescription, now);
        db.Roles.AddRange(owner, manager, doctor);

        // The platform gives a clinic these; nobody of the clinic did (spec 007, FR-027).
        foreach (var role in new[] { owner, manager, doctor })
        {
            db.Record(RoleEntries.Created(role, bySystem: true));
        }

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
                Holders = db.Accounts.Count(a => a.RoleId == r.Id),
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
                    r.HoldsEverything ? PermissionCatalogue.Count : r.Count,
                    r.Holders)),
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
                db.Record(RoleEntries.Created(role));
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
                var before = (role.Name, role.Description, Key: RoleEntries.KeyOf(role));
                role.Describe(cleanName, cleanDescription, Actor(), clock.GetUtcNow());
                var (added, removed) = role.Hold(permissions);
                var change = new RoleChange(role.Id, PermissionCatalogue.InOrder(added), PermissionCatalogue.InOrder(removed));

                // One entry for the edit, however much of the role it changed; none when it changed nothing.
                db.Record(new AuditRecord(
                    AuditActions.RoleUpdated,
                    RoleEntries.Target(role),
                    Changes:
                    [
                        new AuditChange(
                            RoleEntries.Name, before.Name, role.Name, BeforeKey: before.Key, AfterKey: RoleEntries.KeyOf(role)),
                        AuditChange.Of(RoleEntries.Description, before.Description, role.Description),
                        AuditChange.OfSet(RoleEntries.Permissions, change.Added, change.Removed),
                    ]));

                await SaveNamedAsync(cancellation).ConfigureAwait(false);
                return change;
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

                // The entry keeps the role's name and what it held: it is read after the role is gone.
                db.Record(new AuditRecord(
                    AuditActions.RoleDeleted,
                    RoleEntries.Target(role),
                    Changes:
                    [
                        new AuditChange(RoleEntries.Name, role.Name, null, BeforeKey: RoleEntries.KeyOf(role)),
                        AuditChange.OfSet(
                            RoleEntries.Permissions, [], PermissionCatalogue.InOrder(role.Permissions.Select(p => p.Code))),
                    ]));

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

                var previous = account.RoleId is { } held
                    ? await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == held, cancellation).ConfigureAwait(false)
                    : null;

                var hadRole = account.RoleId is not null;
                account.AssignRole(role.Id);
                db.Record(new AuditRecord(
                    AuditActions.AccountRoleChanged,
                    AuditTarget.ForAccount(accountId),
                    Changes:
                    [
                        new AuditChange(
                            RoleEntries.RoleField,
                            previous?.Name,
                            role.Name,
                            BeforeKey: previous is null ? null : RoleEntries.KeyOf(previous),
                            AfterKey: RoleEntries.KeyOf(role)),
                    ]));
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
