using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Scs.BuildingBlocks.Health;

public sealed record HealthCheckResponse(string Name, string Status, long DurationMs);

public sealed record HealthResponse(
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<HealthCheckResponse>? Checks = null);

/// <summary>
/// <c>/health/live</c>: the process answers; no dependency is checked.
/// <c>/health/ready</c>: every check tagged <see cref="ReadyTag"/> passes (FR-004).
/// Shapes are in contracts/platform-api.openapi.yaml.
/// </summary>
public static class HealthEndpoints
{
    public const string ReadyTag = "ready";
    private const string Healthy = "Healthy";
    private const string Unhealthy = "Unhealthy";

    public static IEndpointRouteBuilder MapScsHealth(this IEndpointRouteBuilder root)
    {
        root.MapGet("/health/live", () => Results.Json(new HealthResponse(Healthy)))
            .AllowAnonymous()
            .ExcludeFromDescription();

        root.MapGet("/health/ready", async (HealthCheckService health, CancellationToken cancellation) =>
            {
                var report = await health.CheckHealthAsync(r => r.Tags.Contains(ReadyTag), cancellation);

                var checks = report.Entries
                    .Select(e => new HealthCheckResponse(
                        e.Key,
                        e.Value.Status == HealthStatus.Healthy ? Healthy : Unhealthy,
                        (long)e.Value.Duration.TotalMilliseconds))
                    .ToList();

                var ready = checks.All(c => c.Status == Healthy);

                return Results.Json(
                    new HealthResponse(ready ? Healthy : Unhealthy, checks),
                    statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        return root;
    }
}
