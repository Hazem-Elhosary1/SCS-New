using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Meta;

public class VersionEndpointTests
{
    [Fact]
    public async Task Version_is_open_and_reports_the_running_release()
    {
        const string commit = "3f9c2ab4d5e6f708192a3b4c5d6e7f8091a2b3c4";
        using var factory = new ApiFactory()
            .With("Scs:VERSION", "0.1.0+3f9c2ab")
            .With("Scs:COMMIT", commit)
            .With("Scs:BUILT_AT", "2026-10-10T09:30:00Z");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/meta/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues("X-Trace-Id").Single());

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal("0.1.0+3f9c2ab", root.GetProperty("version").GetString());
        Assert.Equal(commit, root.GetProperty("commit").GetString());
        Assert.Equal(DateTimeOffset.Parse("2026-10-10T09:30:00Z", System.Globalization.CultureInfo.InvariantCulture), root.GetProperty("builtAt").GetDateTimeOffset());
        Assert.Equal("local", root.GetProperty("environment").GetString());
    }

    [Fact]
    public async Task Version_reveals_no_secret()
    {
        using var factory = new ApiFactory().With("Scs:DB:CONNECTION", "Server=db;Password=S3cret-Value!");
        using var client = factory.CreateClient();

        var text = await (await client.GetAsync("/v1/meta/version")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("S3cret-Value!", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Server=", text, StringComparison.Ordinal);
    }
}
