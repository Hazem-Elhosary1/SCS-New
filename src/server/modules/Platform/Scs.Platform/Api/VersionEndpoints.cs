using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.Platform.Contracts;

namespace Scs.Platform.Api;

internal static class VersionEndpoints
{
    /// <summary><c>GET /v1/meta/version</c>: which release is running (FR-005).</summary>
    public static IEndpointRouteBuilder MapVersion(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/meta/version", (IOptions<ScsOptions> options) =>
            {
                var scs = options.Value;
                return TypedResults.Ok(new VersionInfo(scs.Version, scs.Commit, scs.BuiltAt, scs.Environment));
            })
            .AllowAnonymous()
            .WithName("GetVersion");

        return v1;
    }
}
