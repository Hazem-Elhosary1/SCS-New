using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.ClinicData.Contracts;
using Scs.ClinicData.Domain;
using Scs.ClinicData.Infrastructure.Persistence;
using Scs.SharedKernel;

namespace Scs.ClinicData.Infrastructure;

/// <summary>
/// The established clinic's branches (spec 015, FR-026). The main branch is made once, with the
/// clinic; a second attempt is refused by the check here and, in a race, by the database's index.
/// </summary>
internal sealed class Branches(ClinicDataDbContext db, IClinicContext clinic, TimeProvider clock) : IBranches
{
    public async Task<Guid> CreateMainAsync(string nameAr, string nameEn, string address, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(nameAr);
        ArgumentNullException.ThrowIfNull(nameEn);
        ArgumentNullException.ThrowIfNull(address);

        var clinicId = clinic.Require();
        if (await db.Branches.AnyAsync(b => b.ClinicId == clinicId && b.IsMain, cancellation).ConfigureAwait(false))
        {
            throw HasMain();
        }

        var branch = Branch.Main(clinicId, nameAr, nameEn, address, clock.GetUtcNow());
        db.Branches.Add(branch);
        try
        {
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        catch (DbUpdateException failure) when (failure.IsUniqueViolation())
        {
            throw HasMain();
        }

        return branch.Id;
    }

    public async Task UndoMainAsync(Guid branchId, CancellationToken cancellation)
    {
        var clinicId = clinic.Require();
        await db.Branches.Where(b => b.Id == branchId && b.ClinicId == clinicId && b.IsMain)
            .ExecuteDeleteAsync(cancellation).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CancellationToken cancellation)
    {
        var clinicId = clinic.Require();
        return await db.Branches.CountAsync(b => b.ClinicId == clinicId, cancellation).ConfigureAwait(false);
    }

    private static ScsProblemException HasMain() =>
        new(StatusCodes.Status409Conflict, ErrorCodes.ClinicHasMainBranch, "The clinic already has its main branch.");
}
