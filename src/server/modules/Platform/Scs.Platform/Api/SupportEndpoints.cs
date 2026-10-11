using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;

namespace Scs.Platform.Api;

/// <summary>What the help window of the sign-in screens shows. Nothing here is private.</summary>
internal sealed record SupportResponse(string HoursAr, string HoursEn, string Hotline, string Whatsapp, string Email);

internal static class SupportEndpoints
{
    /// <summary>
    /// <c>GET /v1/meta/support</c>: support hours and the three channels (spec 003, FR-050).
    /// Platform settings, held as configuration until the operators' settings screen (spec 024).
    /// </summary>
    public static IEndpointRouteBuilder MapSupport(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/meta/support", (IOptions<ScsOptions> options) =>
            {
                var support = options.Value.Support;
                return TypedResults.Ok(new SupportResponse(
                    support.HoursAr, support.HoursEn, support.Hotline, support.Whatsapp, support.Email));
            })
            .AllowAnonymous()
            .WithName("GetSupport");

        return v1;
    }
}
