using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Modules;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>Endpoints that exist only in tests, to exercise the host pipeline.</summary>
public sealed class ProbeEndpoints : IEndpointContributor
{
    public const string InternalDetail = @"C:\srv\app\Secret.cs SELECT * FROM platform.JobHeartbeat";

    public sealed record ProbeBody(string Name, int Count);

    public void Map(IEndpointRouteBuilder root)
    {
        root.MapGet("/probe/open", () => Results.Ok(new { ok = true })).AllowAnonymous();

        // No authorization metadata at all: must be refused by the fallback policy.
        root.MapGet("/probe/protected", () => Results.Ok(new { ok = true }));

        root.MapGet("/probe/throws", IResult () => throw new InvalidOperationException(InternalDetail))
            .AllowAnonymous();

        root.MapPost("/probe/body", (ProbeBody body) => Results.Ok(body)).AllowAnonymous();

        root.MapGet("/probe/invalid", IResult () => throw ScsProblemException.Validation(
                new Dictionary<string, string[]> { ["email"] = ["REQUIRED"] }))
            .AllowAnonymous();

        root.MapGet("/probe/missing", () => Results.NotFound()).AllowAnonymous();

        root.MapGet("/probe/items/{id}", (string id) => Results.Ok(new { id })).AllowAnonymous();
    }
}
