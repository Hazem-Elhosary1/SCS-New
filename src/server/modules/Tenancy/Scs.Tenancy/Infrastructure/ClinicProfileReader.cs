using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.SharedKernel;
using Scs.Tenancy.Contracts;
using Scs.Tenancy.Infrastructure.Persistence;

namespace Scs.Tenancy.Infrastructure;

/// <summary>Reads the one clinic row isolation lets the current work see: its own.</summary>
internal sealed class ClinicProfileReader(TenancyDbContext db, IClinicContext clinic) : IClinicProfile
{
    public async Task<ClinicProfile> GetOwnAsync(CancellationToken cancellation)
    {
        var id = clinic.Require();
        return await (
                       from c in db.Clinics.AsNoTracking()
                       where c.Id == id
                       select new ClinicProfile(
                           c.Id,
                           c.NameAr,
                           c.NameEn,
                           c.State,
                           c.Specialty,
                           c.Address,
                           c.TaxNumber,
                           c.DefaultLanguage,
                           c.AccountManagerId,
                           c.CreatedBy,
                           c.CreatedAtUtc,
                           db.Directory.Where(e => e.ClinicId == c.Id && e.IsCurrent).Select(e => e.ShortName).FirstOrDefault(),
                           c.RowVersion))
                   .SingleOrDefaultAsync(cancellation)
                   .ConfigureAwait(false)
               ?? throw new ScsProblemException(
                   StatusCodes.Status404NotFound, ErrorCodes.ClinicNotAvailable, "The established clinic has no record.");
    }
}
