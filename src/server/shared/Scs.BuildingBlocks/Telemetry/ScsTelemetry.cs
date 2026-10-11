using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scs.BuildingBlocks.Configuration;

namespace Scs.BuildingBlocks.Telemetry;

/// <summary>
/// Traces and metrics. The application speaks OTLP only, so moving to another host changes the
/// collector, not the code (research R10). Nothing is exported unless <c>SCS_OTEL__ENDPOINT</c>
/// is set, and a collector that is down never stops the platform from serving.
/// </summary>
public static class ScsTelemetry
{
    public const string MeterName = "Scs.Platform";

    private static readonly Meter Meter = new(MeterName);

    /// <summary>Counts job attempts that ended in an exception; the <c>job-failures</c> alarm reads it.</summary>
    public static Counter<long> JobsFailed { get; } =
        Meter.CreateCounter<long>("scs.jobs.failed", description: "Background job attempts that failed");

    public static IServiceCollection AddScsTelemetry(this IServiceCollection services, ScsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Uri.TryCreate(options.Otel.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return services;
        }

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: $"scs-{options.ServiceName}", serviceVersion: options.Version)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", options.Environment)]))
            .WithTracing(tracing => tracing
                // Health probes are noise.
                .AddAspNetCoreInstrumentation(aspnet =>
                    aspnet.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddProcessor(new RouteOnlyProcessor())
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(MeterName)
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint));

        return services;
    }
}

/// <summary>
/// Traces follow the same rule as logs (FR-021): a request is described by its route
/// <em>template</em>. The actual path, the query string and anything taken from headers are
/// removed before a span leaves the process, because path values can be personal data.
/// </summary>
public sealed class RouteOnlyProcessor : BaseProcessor<Activity>
{
    private static readonly string[] Removed =
    [
        "url.path", "url.query", "url.full", "http.target", "http.url",
        "user_agent.original", "http.user_agent", "client.address", "http.client_ip",
    ];

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Outgoing calls keep their address: it is ours, not the caller's. Everything else that
        // describes an HTTP request is an incoming one, whatever kind the span was given.
        if (data.Kind == ActivityKind.Client || data.GetTagItem("http.request.method") is not string method)
        {
            return;
        }

        foreach (var tag in Removed)
        {
            data.SetTag(tag, null);
        }

        // "GET /v1/patients/{id}" when a route matched; the bare method otherwise, never the path.
        data.DisplayName = data.GetTagItem("http.route") is string route ? $"{method} {route}" : method;
    }
}
