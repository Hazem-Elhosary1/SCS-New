using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Health;

[Collection(DatabaseCollection.Name)]
public sealed class ReadyEndpointTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Ready_when_the_database_and_job_storage_answer()
    {
        await sql.EnsureMigratedAsync();
        using var factory = new ApiFactory().With("Scs:DB:CONNECTION", sql.AppConnection);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("Healthy", Check(body, "database"));
        Assert.Equal("Healthy", Check(body, "jobs"));
    }

    [Fact]
    public async Task Alive_but_not_ready_when_the_database_cannot_be_reached()
    {
        // The default fixture address points at nothing, as when the database is stopped.
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var started = System.Diagnostics.Stopwatch.StartNew();
        var ready = await client.GetAsync("/health/ready");
        started.Stop();
        var live = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(5), $"Readiness took {started.Elapsed.TotalSeconds:F1}s to answer.");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        var text = await ready.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("Unhealthy", Check(body, "database"));

        // The reason is not told to the caller.
        Assert.DoesNotContain("Server=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Check(JsonDocument body, string name) =>
        body.RootElement.GetProperty("checks").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == name)
            .GetProperty("status").GetString();
}
