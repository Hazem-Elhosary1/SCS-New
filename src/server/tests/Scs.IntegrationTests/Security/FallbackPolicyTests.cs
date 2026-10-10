using System.Net;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Security;

public sealed class FallbackPolicyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Route_that_declares_nothing_is_refused()
    {
        var response = await _client.GetAsync("/probe/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Route_that_declares_itself_open_is_served()
    {
        var response = await _client.GetAsync("/probe/open");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
