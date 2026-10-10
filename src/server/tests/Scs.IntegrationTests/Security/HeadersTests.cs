using System.Net;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Security;

public class HeadersTests
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/v1/meta/version")]
    [InlineData("/probe/protected")]
    [InlineData("/probe/throws")]
    public async Task Every_response_carries_the_protective_headers(string path)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.False(response.Headers.Contains("Server"));
        Assert.False(response.Headers.Contains("X-Powered-By"));
    }

    [Theory]
    [InlineData("test")]
    [InlineData("production")]
    public async Task Outside_local_HTTPS_is_required_and_remembered(string environment)
    {
        using var factory = new ApiFactory().With("Scs:ENVIRONMENT", environment);

        using var secure = factory.CreateClient(new() { BaseAddress = new Uri("https://app.scs-heta.com"), AllowAutoRedirect = false });
        using var overHttps = await secure.GetAsync("/v1/meta/version");
        Assert.Equal(HttpStatusCode.OK, overHttps.StatusCode);
        Assert.Contains("max-age=31536000", overHttps.Headers.GetValues("Strict-Transport-Security").Single(), StringComparison.Ordinal);

        using var plain = factory.CreateClient(new() { BaseAddress = new Uri("http://app.scs-heta.com"), AllowAutoRedirect = false });
        using var overHttp = await plain.GetAsync("/v1/meta/version");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, overHttp.StatusCode);
        Assert.StartsWith("https://", overHttp.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("production")]
    public async Task Health_probes_are_answered_over_plain_HTTP_inside_the_network(string environment)
    {
        using var factory = new ApiFactory().With("Scs:ENVIRONMENT", environment);
        using var plain = factory.CreateClient(new() { BaseAddress = new Uri("http://10.0.1.15"), AllowAutoRedirect = false });

        using var response = await plain.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Local_does_not_redirect_or_send_HSTS()
    {
        using var factory = new ApiFactory();
        using var plain = factory.CreateClient(new() { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });

        using var response = await plain.GetAsync("/v1/meta/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Cross_origin_calls_are_allowed_in_local_for_the_configured_origins_only()
    {
        using var factory = new ApiFactory().With("Scs:CORS:ORIGINS", "http://localhost:4201");
        using var client = factory.CreateClient();

        async Task<bool> Allowed(string origin)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/meta/version");
            request.Headers.Add("Origin", origin);
            using var response = await client.SendAsync(request);
            return response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) && values.Single() == origin;
        }

        Assert.True(await Allowed("http://localhost:4201"));
        Assert.False(await Allowed("http://evil.example"));
    }
}
