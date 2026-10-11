using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.SharedKernel;
using Scs.Tenancy.Contracts;
using Scs.Tenancy.Domain;
using Scs.Tenancy.Infrastructure.Persistence;

namespace Scs.Tenancy.Infrastructure;

/// <summary>
/// The declared cross-clinic operations on clinics (contracts/isolation.md §5). Everything here
/// runs on the operator connection; the three operations a person performs each write an access
/// record in the same transaction as the change.
/// </summary>
internal sealed partial class ClinicRegistry(
    CrossingDbContext db, TimeProvider clock, ILogger<ClinicRegistry> logger, IServiceScopeFactory? scopes = null)
    : IClinicRegistry
{
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
                join name in db.Directory.AsNoTracking().Where(e => e.IsCurrent) on clinic.Id equals name.ClinicId
                orderby clinic.CreatedAtUtc
                select new ClinicListItem(clinic.Id, name.ShortName, clinic.NameAr, clinic.NameEn, clinic.State, clinic.CreatedAtUtc))
            .ToListAsync(cancellation).ConfigureAwait(false);

        Record(DeclaredCrossings.ListClinics, clinicId: null, operatorId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellation).ConfigureAwait(false);

        return clinics;
    }

    public async Task<IReadOnlyList<Guid>> ListClinicIdsForJobsAsync(CancellationToken cancellation)
    {
        var ids = await db.Clinics.AsNoTracking().OrderBy(c => c.CreatedAtUtc).Select(c => c.Id)
            .ToListAsync(cancellation).ConfigureAwait(false);

        // A crossing by the system, not by a person: logged with a count, not recorded per use.
        LogListedForJobs(logger, ids.Count);
        return ids;
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
}
