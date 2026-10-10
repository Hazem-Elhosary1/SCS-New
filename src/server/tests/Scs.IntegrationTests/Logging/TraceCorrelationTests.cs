using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Logging;

public class TraceCorrelationTests
{
    [Fact]
    public async Task Every_entry_a_request_causes_carries_its_reference()
    {
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/throws");
        request.Headers.Add("traceparent", $"00-{traceId}-00f067aa0ba902b7-01");

        using var response = await client.SendAsync(request);

        var caused = factory.Logs.Lines
            .Where(l => l.Contains("Unhandled failure", StringComparison.Ordinal)
                        || l.Contains("\"route\":\"/probe/throws\"", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, caused.Count);
        Assert.All(caused, line => Assert.Contains($"\"traceId\":\"{traceId}\"", line, StringComparison.Ordinal));
        Assert.Equal(traceId, response.Headers.GetValues("X-Trace-Id").Single());
    }

    [Fact]
    public async Task Concurrent_requests_never_share_a_reference()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var traceIds = Enumerable.Range(0, 25).Select(i => $"{i:x8}{Guid.NewGuid():N}"[..32]).ToList();

        await Task.WhenAll(traceIds.Select(async traceId =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/open");
            request.Headers.Add("traceparent", $"00-{traceId}-00f067aa0ba902b7-01");
            using var response = await client.SendAsync(request);
            Assert.Equal(traceId, response.Headers.GetValues("X-Trace-Id").Single());
        }));

        var completed = factory.Logs.Lines
            .Where(l => l.Contains("\"route\":\"/probe/open\"", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(traceIds.Count, completed.Count);
        foreach (var traceId in traceIds)
        {
            Assert.Single(completed, line => line.Contains($"\"traceId\":\"{traceId}\"", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Entries_carry_the_environment_service_and_version()
    {
        using var factory = new ApiFactory().With("Scs:VERSION", "1.2.3+abc");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/probe/open");

        var line = factory.Logs.Lines.Single(l => l.Contains("\"route\":\"/probe/open\"", StringComparison.Ordinal));
        Assert.Contains("\"env\":\"local\"", line, StringComparison.Ordinal);
        Assert.Contains("\"service\":\"api\"", line, StringComparison.Ordinal);
        Assert.Contains("\"version\":\"1.2.3+abc\"", line, StringComparison.Ordinal);
        Assert.Contains("\"status\":200", line, StringComparison.Ordinal);
        Assert.Contains("\"durationMs\":", line, StringComparison.Ordinal);
    }
}
