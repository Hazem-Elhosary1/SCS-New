using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Tenancy;
using Scs.Tenancy.Contracts;
using Scs.Tenancy.Infrastructure.Persistence;

namespace Scs.Tenancy.Api;

internal static class ClinicEndpoints
{
    public const string RateLimitPolicy = "clinic-lookup";
    public const int RequestsPerMinute = 120;

    /// <summary>
    /// Clinic names are public, but collecting all of them by guessing should not be free.
    /// </summary>
    public static IServiceCollection AddClinicLookupRateLimit(this IServiceCollection services) =>
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(RateLimitPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = RequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                })));

    /// <summary>
    /// <c>GET /v1/clinic</c>: which clinic a patient address belongs to
    /// (contracts/tenancy-api.openapi.yaml). Anonymous; the clinic comes from the patient-surface
    /// header, and its own row is read under isolation.
    /// </summary>
    public static IEndpointRouteBuilder MapClinic(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/clinic", async (HttpContext context, TenancyDbContext db, CancellationToken cancellation) =>
            {
                var location = context.GetClinicLocation();
                var clinic = await db.Clinics.AsNoTracking().SingleAsync(cancellation).ConfigureAwait(false);

                return TypedResults.Ok(new ClinicSummary(
                    location.CurrentShortName, clinic.NameAr, clinic.NameEn, clinic.State, location.Renamed));
            })
            .AllowAnonymous()
            .ForPatientSurface()
            .RequireRateLimiting(RateLimitPolicy)
            .WithName("GetClinic");

        return v1;
    }
}
