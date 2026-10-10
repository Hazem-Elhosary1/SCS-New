using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Health;

public sealed class LiveEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Live_answers_healthy_without_any_dependency()
    {
        // The fixture's database address points nowhere: live must not care.
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.False(body.RootElement.TryGetProperty("checks", out _));
    }
}
