using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Logging;

/// <summary>
/// Traces follow the same rule as logs: the route template, never the request's own data.
/// </summary>
public class TraceRedactionTests
{
    [Fact]
    public async Task Exported_spans_carry_the_route_template_and_no_request_data()
    {
        var spans = new CapturingExporter();
        using var factory = new ApiFactory()
            .With("Scs:OTEL:ENDPOINT", "http://localhost:1")
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.ConfigureOpenTelemetryTracerProvider(tracing =>
                    tracing.AddProcessor(new SimpleActivityExportProcessor(spans)))));
        using var client = factory.CreateClient();

        var path = $"/probe/items/{Uri.EscapeDataString($"{Leak.Name} {Leak.Email}")}";
        var query = $"?name={Uri.EscapeDataString(Leak.Name)}&mobile={Uri.EscapeDataString(Leak.MobileInternational)}&nid={Leak.NationalId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path + query);
        request.Headers.TryAddWithoutValidation("Authorization", Leak.Bearer);
        request.Headers.TryAddWithoutValidation("User-Agent", $"probe {Leak.Email}");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.7");

        using var response = await client.SendAsync(request);
        using var missing = await client.GetAsync($"/no/such/{Uri.EscapeDataString(Leak.Name)}");
        await WaitForAsync(spans, count: 2);

        var server = spans.Activities.Where(a => a.Kind != ActivityKind.Client).ToList();
        Assert.True(server.Count >= 2, "Expected a span for each request.");
        Assert.Contains(server, a => a.DisplayName == "GET /probe/items/{id}");
        Assert.Contains(server, a => (a.GetTagItem("http.route") as string) == "/probe/items/{id}");

        var everything = string.Join('\n', server.Select(Describe));
        foreach (var needle in Leak.Needles.Append("198.51.100.7").Append("probe "))
        {
            Assert.False(
                everything.Contains(needle, StringComparison.OrdinalIgnoreCase),
                $"A span contains '{needle}':\n{everything}");
        }

        Assert.DoesNotContain("url.path", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("url.query", everything, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_probes_are_not_traced()
    {
        var spans = new CapturingExporter();
        using var factory = new ApiFactory()
            .With("Scs:OTEL:ENDPOINT", "http://localhost:1")
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.ConfigureOpenTelemetryTracerProvider(tracing =>
                    tracing.AddProcessor(new SimpleActivityExportProcessor(spans)))));
        using var client = factory.CreateClient();

        using var live = await client.GetAsync("/health/live");
        using var open = await client.GetAsync("/probe/open");
        await WaitForAsync(spans, count: 1);

        var names = spans.Activities.Where(a => a.Kind != ActivityKind.Client).Select(a => a.DisplayName).ToList();
        Assert.Contains("GET /probe/open", names);
        Assert.DoesNotContain(names, name => name.Contains("health", StringComparison.OrdinalIgnoreCase));
    }

    // A span is closed just after its response has been sent, so the test may get there first.
    private static async Task WaitForAsync(CapturingExporter spans, int count)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (spans.Activities.Count(a => a.Kind != ActivityKind.Client) >= count)
            {
                return;
            }

            await Task.Delay(50);
        }
    }

    private static string Describe(Activity activity) =>
        $"{activity.DisplayName} | {activity.OperationName} | " +
        string.Join("; ", activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"));

    private sealed class CapturingExporter : BaseExporter<Activity>
    {
        private readonly List<Activity> _activities = [];

        public IReadOnlyList<Activity> Activities
        {
            get
            {
                lock (_activities)
                {
                    return [.. _activities];
                }
            }
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            lock (_activities)
            {
                foreach (var activity in batch)
                {
                    _activities.Add(activity);
                }
            }

            return ExportResult.Success;
        }
    }
}
