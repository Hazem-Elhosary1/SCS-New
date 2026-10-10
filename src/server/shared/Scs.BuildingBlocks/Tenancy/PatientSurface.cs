using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Scs.BuildingBlocks.Errors;
using Scs.SharedKernel;
using Serilog.Context;

namespace Scs.BuildingBlocks.Tenancy;

/// <summary>What a short name in a patient address leads to.</summary>
/// <param name="ClinicId">The clinic.</param>
/// <param name="CurrentShortName">The clinic's current short name.</param>
/// <param name="Renamed">True when the name asked for is an earlier one.</param>
public sealed record ClinicLocation(Guid ClinicId, string CurrentShortName, bool Renamed);

/// <summary>
/// Finds the clinic a patient address names. Implemented by the Tenancy module; defined here
/// because the shared project may not reference a module. An implementation returns null for
/// every name that leads nowhere a patient may go — unknown, malformed, or a clinic that is not
/// publicly available — so the caller cannot tell those cases apart.
/// </summary>
public interface IClinicLocator
{
    Task<ClinicLocation?> LocateAsync(string? shortName, CancellationToken cancellation);
}

/// <summary>Marks an endpoint as part of the patient site.</summary>
public sealed class PatientSurfaceMetadata
{
    public static PatientSurfaceMetadata Instance { get; } = new();
}

public static class PatientSurfaceExtensions
{
    public const string Header = "X-Scs-Clinic";
    public const int HeaderMaxLength = 60;

    /// <summary>
    /// The endpoint belongs to the patient site: the clinic is taken from the
    /// <c>X-Scs-Clinic</c> header, which the site fills from the first part of its address.
    /// On every other endpoint that header is ignored.
    /// </summary>
    public static TBuilder ForPatientSurface<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(PatientSurfaceMetadata.Instance);
        return builder;
    }

    /// <summary>
    /// Establishes the clinic for patient-surface endpoints. Place after routing. The header
    /// only says which public clinic page is being viewed; it is not a credential, and when the
    /// work already has a clinic and the header names another, the work is refused.
    /// </summary>
    public static IApplicationBuilder UseScsPatientSurface(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<PatientSurfaceMetadata>() is null)
            {
                await next(context);
                return;
            }

            var requested = context.Request.Headers[Header].ToString();
            if (string.IsNullOrWhiteSpace(requested))
            {
                throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.ClinicRequired);
            }

            var locator = context.RequestServices.GetService(typeof(IClinicLocator)) as IClinicLocator;
            var location = requested.Length > HeaderMaxLength || locator is null
                ? null
                : await locator.LocateAsync(requested, context.RequestAborted);

            if (location is null)
            {
                throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.ClinicNotAvailable);
            }

            var clinic = (IClinicContext)context.RequestServices.GetService(typeof(IClinicContext))!;
            clinic.Establish(location.ClinicId, ClinicSource.PatientAddress);
            context.Items[typeof(ClinicLocation)] = location;

            using (LogContext.PushProperty("clinicId", location.ClinicId))
            {
                await next(context);
            }
        });

    /// <summary>The location the patient-surface middleware resolved for this request.</summary>
    public static ClinicLocation GetClinicLocation(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items[typeof(ClinicLocation)] as ClinicLocation
               ?? throw new ScsProblemException(StatusCodes.Status400BadRequest, ErrorCodes.ClinicRequired);
    }
}
