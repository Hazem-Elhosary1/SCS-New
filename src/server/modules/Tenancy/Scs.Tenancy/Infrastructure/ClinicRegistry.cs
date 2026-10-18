using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Audit;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Plans;
using Scs.BuildingBlocks.Tenancy;
using Scs.SharedKernel;
using Scs.SharedKernel.Audit;
using Scs.Tenancy.Contracts;
using Scs.Tenancy.Domain;
using Scs.Tenancy.Infrastructure.Persistence;

namespace Scs.Tenancy.Infrastructure;

/// <summary>
/// The declared cross-clinic operations on clinics (contracts/isolation.md §5). Everything here
/// runs on the operator connection; the operations a person performs each write an access
/// record in the same transaction as the change.
/// </summary>
internal sealed partial class ClinicRegistry(
    CrossingDbContext db, TimeProvider clock, ILogger<ClinicRegistry> logger, IServiceScopeFactory? scopes = null)
    : IClinicRegistry
{
    /// <summary>How many numbered candidates of a derived slug are tried before giving up (research R9).</summary>
    public const int SlugTries = 20;

    private const string AddTurn = "scs:clinic-add";

    public async Task<Guid> RegisterAsync(
        string shortName, string nameAr, string nameEn, string operatorId, CancellationToken cancellation)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!ShortName.TryCreate(shortName, out var canonical, out var code))
        {
            errors["shortName"] = [code!];
        }

        var now = clock.GetUtcNow();
        var (clinic, nameErrors) = Clinic.Create(Guid.CreateVersion7(), nameAr, nameEn, now);
        foreach (var (field, codes) in nameErrors)
        {
            errors[field] = codes;
        }

        if (errors.Count == 0 && await db.Directory.AnyAsync(e => e.ShortName == canonical, cancellation).ConfigureAwait(false))
        {
            errors["shortName"] = [ShortName.Taken];
        }

        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        db.Clinics.Add(clinic!);
        db.Directory.Add(new ClinicDirectoryEntry(canonical, clinic!.Id, now));
        Record(DeclaredCrossings.RegisterClinic, clinic.Id, operatorId, now);
        await SaveAsync(cancellation).ConfigureAwait(false);

        await ProvisionAsync(clinic.Id, cancellation).ConfigureAwait(false);

        return clinic.Id;
    }

    public async Task<IAsyncDisposable> TakeAddTurnAsync(CancellationToken cancellation)
    {
        // A context of its own, so the turn's transaction holds nothing but the lock (research R11).
        var turn = new CrossingDbContext(db.Factory);
        try
        {
            var transaction = await turn.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
            await turn.LockAsync(AddTurn, cancellation).ConfigureAwait(false);
            return new Turn(turn, transaction);
        }
        catch
        {
            await turn.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<RegisteredClinic> RegisterAsync(ClinicRegistration registration, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var now = clock.GetUtcNow();
        var (clinic, errors) = Clinic.Register(
            Guid.CreateVersion7(), registration.NameAr, registration.NameEn, registration.Specialty, registration.Address,
            registration.TaxNumber, registration.OperatorId, now);
        if (clinic is null)
        {
            throw ScsProblemException.Validation(errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal));
        }

        var derived = ShortName.Derive(clinic.NameEn, clinic.NameAr);
        var taken = await db.Directory.AsNoTracking()
            .Where(e => e.ShortName == derived || e.ShortName.StartsWith(derived + "-"))
            .Select(e => e.ShortName)
            .ToListAsync(cancellation).ConfigureAwait(false);

        db.Clinics.Add(clinic);
        for (var number = 1; ; number++)
        {
            var slug = ShortName.Candidate(derived, number);
            if (taken.Contains(slug, StringComparer.Ordinal))
            {
                continue;
            }

            var entry = new ClinicDirectoryEntry(slug, clinic.Id, now);
            db.Directory.Add(entry);
            Record(DeclaredCrossings.RegisterClinic, clinic.Id, registration.OperatorId, now);
            try
            {
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            }
            catch (DbUpdateException clash) when (number < SlugTries && clash.IsUniqueViolation())
            {
                // Another add took this name at the same moment: the next number (research R9).
                db.Entry(entry).State = EntityState.Detached;
                foreach (var access in db.ChangeTracker.Entries<CrossClinicAccess>().Where(a => a.State == EntityState.Added).ToList())
                {
                    access.State = EntityState.Detached;
                }

                taken.Add(slug);
                continue;
            }

            await ProvisionAsync(clinic.Id, cancellation).ConfigureAwait(false);
            return new RegisteredClinic(clinic.Id, slug);
        }
    }

    public async Task ActivateAsync(Guid clinicId, ClinicCreation creation, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        var clinic = await FindAsync(clinicId, visibleOnly: false, cancellation).ConfigureAwait(false);
        var slug = await db.Directory.Where(e => e.ClinicId == clinicId && e.IsCurrent).Select(e => e.ShortName)
            .SingleAsync(cancellation).ConfigureAwait(false);

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            clinic.Activate();
            var now = clock.GetUtcNow();
            Record(DeclaredCrossings.UpdateClinic, clinicId, creation.OperatorId, now);
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

            var record = new AuditRecord(
                AuditActions.ClinicCreated,
                Target(clinic),
                Facts: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["slug"] = slug,
                    ["plan"] = creation.PlanCode ?? "",
                    ["specialty"] = clinic.Specialty ?? "",
                },
                Changes:
                [
                    AuditChange.Of("nameAr", null, clinic.NameAr),
                    AuditChange.Of("nameEn", null, clinic.NameEn),
                    AuditChange.Of("address", null, clinic.Address),
                    AuditChange.Of("taxNumber", null, clinic.TaxNumber),
                    AuditChange.Of("specialty", null, clinic.Specialty),
                    AuditChange.Of("defaultLanguage", null, clinic.DefaultLanguage),
                    AuditChange.Of("plan", null, creation.PlanCode),
                    AuditChange.Of("trialEndsAt", null, creation.TrialEndsAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                    AuditChange.Of("ownerName", null, creation.OwnerName),
                    AuditChange.Of("ownerEmail", null, creation.OwnerEmail),
                    AuditChange.Of("ownerMobile", null, creation.OwnerMobile),
                ]);
            await db.RecordForClinicAsync(clinicId, record, cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(record, cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    public async Task DiscardAsync(Guid clinicId, string operatorId, CancellationToken cancellation)
    {
        var clinic = await db.Clinics.SingleOrDefaultAsync(c => c.Id == clinicId, cancellation).ConfigureAwait(false);
        if (clinic is not null)
        {
            // The stored state, not this scope's copy: an activation that failed to save has already
            // marked the copy active (research R2; T097).
            await db.Entry(clinic).ReloadAsync(cancellation).ConfigureAwait(false);
        }

        if (clinic is null || clinic.State != Clinic.SettingUp)
        {
            return;
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            clinic.Discard();
            foreach (var name in await db.Directory.Where(e => e.ClinicId == clinicId && e.IsCurrent).ToListAsync(cancellation).ConfigureAwait(false))
            {
                name.Retire();
            }

            Record(DeclaredCrossings.UpdateClinic, clinicId, operatorId, clock.GetUtcNow());
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    public async Task CheckProfileAsync(
        Guid clinicId, byte[] version, ClinicProfileChange change, Dictionary<string, string[]> errors, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(errors);

        // A copy nobody saves: the edit is tried on it for its refusals only.
        var clinic = await db.Clinics.AsNoTracking().SingleOrDefaultAsync(c => c.Id == clinicId, cancellation).ConfigureAwait(false);
        if (clinic is null || ClinicStates.Invisible.Contains(clinic.State))
        {
            throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "No such clinic.");
        }

        Expect(clinic, version);
        clinic.Edit(
            change.NameAr, change.NameEn, change.Address, new TaxNumberEdit(change.TaxNumberSent, change.TaxNumber), change.Specialty,
            change.DefaultLanguage, errors);
    }

    public async Task<byte[]> UpdateProfileAsync(
        Guid clinicId, byte[] version, ClinicProfileChange change, bool ownerChanged, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(change);
        var clinic = await FindAsync(clinicId, visibleOnly: true, cancellation).ConfigureAwait(false);
        Expect(clinic, version);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var changes = clinic.Edit(
            change.NameAr, change.NameEn, change.Address, new TaxNumberEdit(change.TaxNumberSent, change.TaxNumber), change.Specialty,
            change.DefaultLanguage, errors);
        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        if (changes.Count == 0)
        {
            return ownerChanged ? await TouchAsync(clinic, change.OperatorId, cancellation).ConfigureAwait(false) : clinic.RowVersion;
        }

        var record = new AuditRecord(
            AuditActions.ClinicUpdated,
            Target(clinic),
            Facts: new Dictionary<string, object?>(StringComparer.Ordinal) { ["note"] = Note(change.Note) },
            Changes: [.. changes.Select(c => AuditChange.Of(c.Field, c.Before, c.After))]);
        return await SaveChangeAsync(clinic, record, change.OperatorId, cancellation).ConfigureAwait(false);
    }

    public async Task<byte[]> AssignAccountManagerAsync(
        Guid clinicId, byte[] version, Guid? accountManagerId, string? nameBefore, string? nameAfter, string operatorId,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(version);
        var clinic = await FindAsync(clinicId, visibleOnly: true, cancellation).ConfigureAwait(false);
        Expect(clinic, version);

        if (!clinic.AssignAccountManager(accountManagerId))
        {
            return clinic.RowVersion;
        }

        var record = new AuditRecord(
            AuditActions.ClinicAccountManagerAssigned,
            Target(clinic),
            Changes: [AuditChange.Of("accountManager", nameBefore, nameAfter)]);
        return await SaveChangeAsync(clinic, record, operatorId, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the new clinic what every clinic starts with. Each module's part runs in a scope of
    /// its own with the clinic established — under isolation, not on this operator connection.
    /// A failure is the caller's to see; the parts are safe to run again.
    /// </summary>
    private async Task ProvisionAsync(Guid clinicId, CancellationToken cancellation)
    {
        if (scopes is null)
        {
            return;
        }

        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            scope.ServiceProvider.GetRequiredService<IClinicContext>().Establish(clinicId, ClinicSource.Job);

            foreach (var provisioner in scope.ServiceProvider.GetServices<IClinicProvisioner>())
            {
                await provisioner.ProvisionAsync(cancellation).ConfigureAwait(false);
            }
        }
    }

    public async Task RenameAsync(Guid clinicId, string newShortName, string operatorId, CancellationToken cancellation)
    {
        if (!ShortName.TryCreate(newShortName, out var canonical, out var code))
        {
            throw ScsProblemException.Validation(new Dictionary<string, string[]> { ["shortName"] = [code!] });
        }

        var names = await db.Directory.Where(e => e.ClinicId == clinicId).ToListAsync(cancellation).ConfigureAwait(false);
        if (names.Count == 0)
        {
            throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "No such clinic.");
        }

        var current = names.Single(e => e.IsCurrent);
        if (current.ShortName == canonical)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var earlier = names.SingleOrDefault(e => e.ShortName == canonical);
        if (earlier is null && await db.Directory.AnyAsync(e => e.ShortName == canonical, cancellation).ConfigureAwait(false))
        {
            throw ScsProblemException.Validation(new Dictionary<string, string[]> { ["shortName"] = [ShortName.Taken] });
        }

        // Retire first and save, so the "one current name per clinic" index is never broken
        // part-way; both steps are inside one transaction.
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            current.Retire();
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

            if (earlier is null)
            {
                db.Directory.Add(new ClinicDirectoryEntry(canonical, clinicId, now));
            }
            else
            {
                // One of the clinic's own earlier names: it becomes current again.
                earlier.MakeCurrent();
            }

            Record(DeclaredCrossings.RenameClinic, clinicId, operatorId, now);
            await SaveAsync(cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ClinicListItem>> ListAsync(string operatorId, CancellationToken cancellation)
    {
        var clinics = await (
                from clinic in db.Clinics.AsNoTracking()
                where !ClinicStates.Invisible.Contains(clinic.State)
                join name in db.Directory.AsNoTracking().Where(e => e.IsCurrent) on clinic.Id equals name.ClinicId
                orderby clinic.CreatedAtUtc
                select new ClinicListItem(
                    clinic.Id, name.ShortName, clinic.NameAr, clinic.NameEn, clinic.State, clinic.CreatedAtUtc, clinic.CreatedBy,
                    clinic.AccountManagerId))
            .ToListAsync(cancellation).ConfigureAwait(false);

        Record(DeclaredCrossings.ListClinics, clinicId: null, operatorId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

        return clinics;
    }

    public async Task<IReadOnlyList<Guid>> ListClinicIdsForJobsAsync(CancellationToken cancellation)
    {
        var ids = await db.Clinics.AsNoTracking()
            .Where(c => !ClinicStates.Invisible.Contains(c.State))
            .OrderBy(c => c.CreatedAtUtc).Select(c => c.Id)
            .ToListAsync(cancellation).ConfigureAwait(false);

        // A crossing by the system, not by a person: logged with a count, not recorded per use.
        LogListedForJobs(logger, ids.Count);
        return ids;
    }

    private static AuditTarget Target(Clinic clinic) => new(AuditTarget.Clinic, clinic.Id, clinic.NameAr);

    /// <summary>The note as the trail keeps it: trimmed; its length was checked by the route (≤ 500).</summary>
    private static string Note(string? note) => (note ?? "").Trim();

    private static void Expect(Clinic clinic, byte[] version)
    {
        if (!clinic.RowVersion.AsSpan().SequenceEqual(version))
        {
            throw ChangedElsewhere();
        }
    }

    private static ScsProblemException ChangedElsewhere() =>
        new(StatusCodes.Status409Conflict, ErrorCodes.ClinicChangedElsewhere, "The clinic changed since it was read.");

    private async Task<Clinic> FindAsync(Guid clinicId, bool visibleOnly, CancellationToken cancellation)
    {
        var clinic = await db.Clinics.SingleOrDefaultAsync(c => c.Id == clinicId, cancellation).ConfigureAwait(false);
        if (clinic is null || (visibleOnly && ClinicStates.Invisible.Contains(clinic.State)))
        {
            throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "No such clinic.");
        }

        return clinic;
    }

    /// <summary>
    /// Moves the clinic's version and nothing else: its owner changed (recorded by Identity as
    /// <c>clinic.owner-updated</c>), so a form read before is stale (FR-021).
    /// </summary>
    private async Task<byte[]> TouchAsync(Clinic clinic, string operatorId, CancellationToken cancellation)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            Record(DeclaredCrossings.UpdateClinic, clinic.Id, operatorId, clock.GetUtcNow());
            db.Entry(clinic).Property(c => c.NameAr).IsModified = true;
            try
            {
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw ChangedElsewhere();
            }

            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        return clinic.RowVersion;
    }

    /// <summary>Saves a change of the clinic's record with its entry in both trails, in one transaction.</summary>
    private async Task<byte[]> SaveChangeAsync(Clinic clinic, AuditRecord record, string operatorId, CancellationToken cancellation)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellation).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            Record(DeclaredCrossings.UpdateClinic, clinic.Id, operatorId, clock.GetUtcNow());
            try
            {
                await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw ChangedElsewhere();
            }

            await db.RecordForClinicAsync(clinic.Id, record, cancellation).ConfigureAwait(false);
            await db.RecordForPlatformAsync(record, cancellation).ConfigureAwait(false);
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        }

        return clinic.RowVersion;
    }

    private void Record(string operation, Guid? clinicId, string operatorId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        db.Access.Add(new CrossClinicAccess(
            operation, clinicId, operatorId.Trim(), Activity.Current?.TraceId.ToHexString(), now));
    }

    private async Task SaveAsync(CancellationToken cancellation)
    {
        try
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (db.ChangeTracker.Entries<ClinicDirectoryEntry>().Any(e => e.State == EntityState.Added))
        {
            // Two operators chose the same name at the same moment; the key decided.
            throw ScsProblemException.Validation(new Dictionary<string, string[]> { ["shortName"] = [ShortName.Taken] });
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Listed {count} clinic ids for per-clinic jobs")]
    private static partial void LogListedForJobs(ILogger logger, int count);

    /// <summary>The add's turn: the lock is held by its transaction until disposed.</summary>
    private sealed class Turn(CrossingDbContext context, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            await context.DisposeAsync().ConfigureAwait(false);
        }
    }
}
