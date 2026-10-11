using System.Net;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Security;

public class ApiDocsTests
{
    private const string Document = "/openapi/v1.json";

    [Theory]
    // When the document is not mapped the route does not exist, and a route that does not
    // exist is refused like any other undeclared route.
    [InlineData("local", "true", HttpStatusCode.OK)]
    [InlineData("test", "true", HttpStatusCode.OK)]
    [InlineData("local", "false", HttpStatusCode.Unauthorized)]
    [InlineData("local", null, HttpStatusCode.Unauthorized)]
    [InlineData("production", "true", HttpStatusCode.Unauthorized)]
    [InlineData("production", "false", HttpStatusCode.Unauthorized)]
    public async Task Interface_documentation_is_never_reachable_in_production(
        string environment, string? enabled, HttpStatusCode expected)
    {
        using var factory = new ApiFactory()
            .With("Scs:ENVIRONMENT", environment)
            .With("Scs:APIDOCS:ENABLED", enabled);
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(Document);

        Assert.Equal(expected, response.StatusCode);
    }
}
