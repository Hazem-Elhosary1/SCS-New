using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Tenancy;
using Scs.Tenancy.Contracts;
using Scs.Tenancy.Domain;
using Scs.Tenancy.Infrastructure.Persistence;

namespace Scs.Tenancy.Infrastructure;

/// <summary>
/// Turns the short name in a patient address into a clinic. It needs no privilege: it reads the
/// directory of names, which belongs to no clinic, and then the clinic's own row under
/// isolation. Unknown, malformed and "not publicly available" all come back as nothing
/// (spec 002, FR-010, FR-033).
/// </summary>
internal sealed class ClinicLocator(TenancyDbContext db, IClinicContext clinic) : IClinicLocator
{
    public async Task<ClinicLocation?> LocateAsync(string? shortName, CancellationToken cancellation)
    {
        var canonical = ShortName.Canonicalise(shortName);
        if (!ShortName.CouldBeAName(canonical))
        {
            // Cannot be anyone's name: do not ask the database.
            return null;
        }

        var entry = await db.Directory.AsNoTracking()
            .Where(e => e.ShortName == canonical)
            .Select(e => new { e.ClinicId, e.IsCurrent })
            .SingleOrDefaultAsync(cancellation).ConfigureAwait(false);

        if (entry is null)
        {
            return null;
        }

        // From here the work is this clinic's, so its own row can be read under isolation.
        clinic.Establish(entry.ClinicId, ClinicSource.PatientAddress);

        var state = await db.Clinics.AsNoTracking().Select(c => c.State)
            .SingleOrDefaultAsync(cancellation).ConfigureAwait(false);

        if (state is null || ClinicStates.NotPubliclyAvailable.Contains(state))
        {
            return null;
        }

        var current = entry.IsCurrent
            ? canonical
            : await db.Directory.AsNoTracking()
                .Where(e => e.ClinicId == entry.ClinicId && e.IsCurrent)
                .Select(e => e.ShortName)
                .SingleAsync(cancellation).ConfigureAwait(false);

        return new ClinicLocation(entry.ClinicId, current, Renamed: !entry.IsCurrent);
    }
}
