using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.Operators.Infrastructure;

internal sealed record RoleActions(bool Edit, bool Delete);

/// <param name="Operators">How many accounts hold the role, in any state.</param>
/// <param name="Permissions">How many permissions it holds.</param>
internal sealed record RoleRow(
    Guid Id, string Name, string? SystemKey, string? Description, int Operators, bool InUse, int Permissions, RoleActions Can);

internal sealed record RoleCards(int Total, int InUse, int Custom, int System);

internal sealed record RolesList(IReadOnlyList<RoleRow> Items, RoleCards Cards);

/// <summary>What a new role based on this one would start with, for whoever is asking (FR-017).</summary>
internal sealed record RoleCopy(IReadOnlyList<string> Codes, IReadOnlyList<string> Left);

/// <param name="ReadOnlyReason"><c>system</c>, <c>own-role</c>, or null when the caller may edit it.</param>
internal sealed record RoleView(
    Guid Id, string Name, string? Description, string? SystemKey, IReadOnlyList<string> Codes, string Version,
    string? ReadOnlyReason, RoleCopy Copy);

/// <param name="Reason">Why it may not be given by the caller: <c>holds-more</c>, <c>super-admin-only</c>, or null.</param>
internal sealed record RoleOption(Guid Id, string Name, string? SystemKey, IReadOnlyList<string> Codes, bool Grantable, string? Reason);

internal sealed record CataloguePermission(string Code, string? Needs, bool MayGrant);

internal sealed record CatalogueGroup(string Key, IReadOnlyList<CataloguePermission> Items);

internal sealed record RoleInput(string? Name, string? Description, IReadOnlyList<string>? Codes, Guid? BasedOnId, string? Note, string? Version);

/// <summary>
/// The roles of the operators console (spec 011, User Story 6; FR-010 to FR-021): the list, the
/// form's reads, and making, editing and deleting a custom role. The three roles of the
/// platform are read and never written. Every write asks the one rule of
/// <see cref="RoleGrants"/>, and is recorded with its change in one transaction.
/// </summary>
internal sealed class Roles(OperatorsDbContext db, IOperatorAccess access, TimeProvider clock)
{
    public const string ReadOnlySystem = "system";
    public const string ReadOnlyOwnRole = "own-role";

    private const string Required = PasswordRules.Required;
    private const string Invalid = "INVALID";
    private const string TooLong = PasswordRules.TooLong;

    // ---------------------------------------------------------------- reads

    /// <summary>System roles first, then custom ones, oldest first.</summary>
    public async Task<RolesList> ListAsync(CancellationToken cancellation)
    {
        var manages = await access.HasAsync(OperatorPermissions.OperatorsManageRoles, cancellation).ConfigureAwait(false);

        var rows = await db.Roles.AsNoTracking()
            .Where(r => r.DeletedAtUtc == null)
            .OrderBy(r => r.SystemKey == null)
            .ThenBy(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .Select(r => new
            {
                r.Id, r.Name, r.SystemKey, r.Description,
                Operators = db.Operators.Count(o => o.RoleId == r.Id),
                Permissions = db.RolePermissions.Count(p => p.RoleId == r.Id),
            })
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        // The platform's own three in the order the console names them, whatever their rows' times.
        var ordered = rows
            .OrderBy(r => r.SystemKey is null ? int.MaxValue : OperatorPermissions.SystemRoles.Keys.ToList().IndexOf(r.SystemKey))
            .ToList();

        List<RoleRow> items =
        [
            .. ordered.Select(r => new RoleRow(
                r.Id, r.Name, r.SystemKey, r.Description, r.Operators, r.Operators > 0, r.Permissions,
                new RoleActions(manages && r.SystemKey is null, manages && r.SystemKey is null))),
        ];

        return new RolesList(
            items,
            new RoleCards(
                items.Count, items.Count(r => r.InUse), items.Count(r => r.SystemKey is null), items.Count(r => r.SystemKey is not null)));
    }

    public async Task<RoleView?> ReadAsync(Guid id, Guid callerId, CancellationToken cancellation)
    {
        var role = await db.Roles.AsNoTracking().Include(r => r.Permissions)
            .SingleOrDefaultAsync(r => r.Id == id && r.DeletedAtUtc == null, cancellation)
            .ConfigureAwait(false);
        if (role is null)
        {
            return null;
        }

        var granter = await GranterAsync(callerId, cancellation).ConfigureAwait(false);
        var (taken, left) = RoleGrants.Copy(granter, role.Codes);

        return new RoleView(
            role.Id, role.Name, role.Description, role.SystemKey, role.Codes, Convert.ToBase64String(role.RowVersion),
            role.IsSystem ? ReadOnlySystem : RoleGrants.MayEdit(granter, role.Id) ? null : ReadOnlyOwnRole,
            new RoleCopy(taken, left));
    }

    /// <summary>The role picker of an operator's form: every role, and whether this person may give it.</summary>
    public async Task<IReadOnlyList<RoleOption>> OptionsAsync(Guid callerId, CancellationToken cancellation)
    {
        var granter = await GranterAsync(callerId, cancellation).ConfigureAwait(false);
        var roles = await db.Roles.AsNoTracking().Include(r => r.Permissions)
            .Where(r => r.DeletedAtUtc == null)
            .OrderBy(r => r.SystemKey == null)
            .ThenBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        return
        [
            .. roles
                .OrderBy(r => r.SystemKey is null ? int.MaxValue : OperatorPermissions.SystemRoles.Keys.ToList().IndexOf(r.SystemKey))
                .Select(r =>
                {
                    var reason = RoleGrants.WhyNotGrantable(granter, r.SystemKey, r.Codes);
                    return new RoleOption(r.Id, r.Name, r.SystemKey, r.Codes, reason is null, reason);
                }),
        ];
    }

    /// <summary>The catalogue in its nine groups, with what this person may tick.</summary>
    public async Task<IReadOnlyList<CatalogueGroup>> CatalogueAsync(Guid callerId, CancellationToken cancellation)
    {
        var granter = await GranterAsync(callerId, cancellation).ConfigureAwait(false);
        return
        [
            .. OperatorPermissions.Groups.All.Select(group => new CatalogueGroup(
                group,
                [
                    .. OperatorPermissions.All
                        .Where(permission => permission.Group == group)
                        .Select(permission => new CataloguePermission(
                            permission.Code, permission.Needs, RoleGrants.MayAdd(granter, permission.Code))),
                ])),
        ];
    }

    // ---------------------------------------------------------------- writes

    public async Task<Guid> CreateAsync(RoleInput input, Guid callerId, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(input);
        Refuse(Check(input, editing: false));
        var granter = await GranterAsync(callerId, cancellation).ConfigureAwait(false);

        var refused = RoleGrants.NotAddable(granter, [], input.Codes!);
        if (refused.Count > 0)
        {
            throw NotHeld(refused);
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await RefuseTakenNameAsync(input.Name!, null, cancellation).ConfigureAwait(false);

            var basedOn = input.BasedOnId is { } source
                ? await db.Roles.AsNoTracking().Where(r => r.Id == source && r.DeletedAtUtc == null)
                    .Select(r => new { r.Name, r.SystemKey }).SingleOrDefaultAsync(cancellation).ConfigureAwait(false)
                : null;

            var role = Role.Create(input.Name!, input.Description, input.Codes!, callerId, clock.GetUtcNow());
            db.Roles.Add(role);
            await SaveAsync(cancellation).ConfigureAwait(false);

            await db.RecordForPlatformAsync(
                    new AuditRecord(
                        AuditActions.OperatorRoleCreated,
                        Target(role),
                        basedOn is null
                            ? null
                            : new Dictionary<string, object?>(StringComparer.Ordinal) { ["basedOn"] = basedOn.SystemKey ?? basedOn.Name },
                        [
                            AuditChange.Of("name", null, role.Name),
                            AuditChange.Of("description", null, role.Description),
                            AuditChange.OfSet("permissions", role.Codes, []),
                        ]),
                    cancellation)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
            return role.Id;
        }
    }

    /// <summary>
    /// Edits a custom role. Its holders keep their sessions: what they may do follows at their
    /// next request (FR-005).
    /// </summary>
    public async Task EditAsync(Guid id, RoleInput input, Guid callerId, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(input);
        var granter = await GranterAsync(callerId, cancellation).ConfigureAwait(false);

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var role = await FindAsync(id, cancellation).ConfigureAwait(false);
            RefuseSystem(role);
            if (!RoleGrants.MayEdit(granter, role.Id))
            {
                throw new ScsProblemException(StatusCodes.Status403Forbidden, ErrorCodes.OpsOwnRoleEdit);
            }

            Refuse(Check(input, editing: true));
            if (Convert.ToBase64String(role.RowVersion) != input.Version)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsChangedElsewhere);
            }

            // What the role already held stays, even where this person does not hold it (ق-63).
            var held = role.Codes;
            var refused = RoleGrants.NotAddable(granter, held, input.Codes!);
            if (refused.Count > 0)
            {
                throw NotHeld(refused);
            }

            await RefuseTakenNameAsync(input.Name!, role.Id, cancellation).ConfigureAwait(false);

            var was = new { role.Name, role.Description };
            role.Describe(input.Name!, input.Description);
            role.Hold(input.Codes!);
            await SaveAsync(cancellation).ConfigureAwait(false);

            var now = role.Codes;
            List<AuditChange> changes =
            [
                AuditChange.Of("name", was.Name, role.Name),
                AuditChange.Of("description", was.Description, role.Description),
                AuditChange.OfSet("permissions", now.Except(held, StringComparer.Ordinal), held.Except(now, StringComparer.Ordinal)),
            ];
            changes.RemoveAll(change => change.IsEmpty);

            if (changes.Count > 0)
            {
                await db.RecordForPlatformAsync(
                        new AuditRecord(AuditActions.OperatorRoleEdited, Target(role), Note(input.Note), changes), cancellation)
                    .ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>Hides a custom role that nobody holds. What was recorded about it keeps its name.</summary>
    public async Task DeleteAsync(Guid id, string? note, CancellationToken cancellation)
    {
        if ((note ?? "").Trim().Length > OperatorManagement.NoteMaxLength)
        {
            Refuse(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["note"] = [TooLong] });
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // Nobody is given the role while it is being counted.
            await SuperAdminGuard.LockRoleHoldersAsync(db, cancellation).ConfigureAwait(false);

            var role = await FindAsync(id, cancellation).ConfigureAwait(false);
            RefuseSystem(role);

            var holders = await db.Operators.AsNoTracking().CountAsync(o => o.RoleId == id, cancellation).ConfigureAwait(false);
            if (holders > 0)
            {
                throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsRoleInUse)
                {
                    Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["operators"] = holders },
                };
            }

            var target = Target(role);
            role.Delete(clock.GetUtcNow());
            await SaveAsync(cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(new AuditRecord(AuditActions.OperatorRoleDeleted, target, Note(note)), cancellation)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------- pieces

    private async Task<Granter> GranterAsync(Guid callerId, CancellationToken cancellation)
    {
        var grant = await access.GetAsync(cancellation).ConfigureAwait(false);
        return new Granter(callerId, grant.RoleId, grant.IsSuperAdmin, grant.Codes);
    }

    private async Task<Role> FindAsync(Guid id, CancellationToken cancellation) =>
        await db.Roles.Include(r => r.Permissions)
            .SingleOrDefaultAsync(r => r.Id == id && r.DeletedAtUtc == null, cancellation)
            .ConfigureAwait(false)
        ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    private static void RefuseSystem(Role role)
    {
        if (role.IsSystem)
        {
            throw new ScsProblemException(StatusCodes.Status403Forbidden, ErrorCodes.OpsSystemRole);
        }
    }

    /// <summary>
    /// A name is one role's: compared trimmed and without case among the roles that are not
    /// deleted, and never one of the names the console gives the platform's own three.
    /// </summary>
    private async Task RefuseTakenNameAsync(string name, Guid? exceptId, CancellationToken cancellation)
    {
        var key = Role.KeyOf(name);
        var system = OperatorPermissions.SystemRoles.Keys
            .SelectMany(k => new[] { k, OperatorPermissions.SystemRoles.NameOf(k).Ar, OperatorPermissions.SystemRoles.NameOf(k).En })
            .Any(taken => Role.KeyOf(taken) == key);

        if (system || await db.Roles.AsNoTracking()
                .AnyAsync(r => r.NameKey == key && r.Id != exceptId && r.DeletedAtUtc == null, cancellation)
                .ConfigureAwait(false))
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsRoleNameTaken);
        }
    }

    private async Task SaveAsync(CancellationToken cancellation)
    {
        try
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsChangedElsewhere);
        }
        catch (DbUpdateException taken) when (taken.IsUniqueViolation())
        {
            throw new ScsProblemException(StatusCodes.Status409Conflict, ErrorCodes.OpsRoleNameTaken);
        }
    }

    private static Dictionary<string, string[]> Check(RoleInput input, bool editing)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var name = (input.Name ?? "").Trim();
        if (name.Length == 0)
        {
            errors["name"] = [Required];
        }
        else if (name.Length > Role.NameMaxLength)
        {
            errors["name"] = [TooLong];
        }

        if ((input.Description ?? "").Trim().Length > Role.DescriptionMaxLength)
        {
            errors["description"] = [TooLong];
        }

        if (input.Codes is not { Count: > 0 })
        {
            errors["codes"] = [Required];
        }
        else if (!input.Codes.All(OperatorPermissions.IsKnown))
        {
            errors["codes"] = [Invalid];
        }

        if (editing)
        {
            if (string.IsNullOrEmpty(input.Version))
            {
                errors["version"] = [Required];
            }

            if ((input.Note ?? "").Trim().Length > OperatorManagement.NoteMaxLength)
            {
                errors["note"] = [TooLong];
            }
        }

        return errors;
    }

    private static void Refuse(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }
    }

    private static ScsProblemException NotHeld(IReadOnlyList<string> codes) =>
        new(StatusCodes.Status403Forbidden, ErrorCodes.OpsPermissionsNotHeld)
        {
            Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["permissions"] = codes },
        };

    private static Dictionary<string, object?>? Note(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : new Dictionary<string, object?>(StringComparer.Ordinal) { ["note"] = note.Trim() };

    private static AuditTarget Target(Role role) => new(AuditTarget.OperatorRole, role.Id, role.Name);
}
