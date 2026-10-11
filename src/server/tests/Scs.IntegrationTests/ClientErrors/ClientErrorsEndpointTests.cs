using System.Net;
using System.Text;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.ClientErrors;

/// <summary>Contract tests for <c>POST /v1/client-errors</c> (contracts/platform-api.openapi.yaml).</summary>
public class ClientErrorsEndpointTests
{
    private const string Path = "/v1/client-errors";

    private static StringContent Body(string json) => new(json, Encoding.UTF8, "application/json");

    private static string Valid(string extra = "") =>
        $$"""{"app":"clinic","version":"0.1.0+3f9c2ab","message":"Cannot read properties of undefined"{{extra}}}""";

    [Fact]
    public async Task Valid_report_is_accepted_and_logged_as_coming_from_the_front_end()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(Path, Body(Valid(
            ""","route":"/patients/:id","stack":"at save (main.js:1:2)","traceId":"4bf92f3577b34da6a3ce929d0e0e4736","language":"ar" """)));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues("X-Trace-Id").Single());

        var entry = factory.Logs.Lines.Single(l => l.Contains("Front-end failure", StringComparison.Ordinal));
        Assert.Contains("\"service\":\"web-clinic\"", entry, StringComparison.Ordinal);
        Assert.Contains("\"clientRoute\":\"/patients/:id\"", entry, StringComparison.Ordinal);
        Assert.Contains("\"clientTraceId\":\"4bf92f3577b34da6a3ce929d0e0e4736\"", entry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task What_the_front_end_reports_is_scrubbed_like_any_other_entry()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(Path, Body(
            $$"""{"app":"ops","version":"1","message":"Save failed for {{Leak.Email}} / {{Leak.MobileLocal}}"}"""));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var entry = factory.Logs.Lines.Single(l => l.Contains("Front-end failure", StringComparison.Ordinal));
        Assert.DoesNotContain(Leak.Email, entry, StringComparison.Ordinal);
        Assert.DoesNotContain(Leak.MobileLocal, entry, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"app":"clinic","version":"1","message":"m","userId":"7"}""", "userId", "UNKNOWN")]
    [InlineData("""{"app":"desktop","version":"1","message":"m"}""", "app", "INVALID")]
    [InlineData("""{"version":"1","message":"m"}""", "app", "REQUIRED")]
    [InlineData("""{"app":"clinic","message":"m"}""", "version", "REQUIRED")]
    [InlineData("""{"app":"clinic","version":"1"}""", "message", "REQUIRED")]
    [InlineData("""{"app":"clinic","version":"1","message":"   "}""", "message", "REQUIRED")]
    [InlineData("""{"app":"clinic","version":"1","message":7}""", "message", "INVALID")]
    [InlineData("""{"app":"clinic","version":"1","message":"m","traceId":"not-a-trace"}""", "traceId", "INVALID")]
    [InlineData("""{"app":"clinic","version":"1","message":"m","language":"fr"}""", "language", "INVALID")]
    public async Task Invalid_report_is_refused_with_field_codes(string json, string field, string code)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(Path, Body(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VALIDATION_FAILED", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(code, problem.RootElement.GetProperty("errors").GetProperty(field)[0].GetString());
        Assert.DoesNotContain(factory.Logs.Lines, l => l.Contains("Front-end failure", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("version", 41)]
    [InlineData("route", 201)]
    [InlineData("message", 1001)]
    [InlineData("stack", 8001)]
    public async Task Over_long_field_is_refused(string field, int length)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var report = new Dictionary<string, string> { ["app"] = "clinic", ["version"] = "1", ["message"] = "m" };
        report[field] = new string('x', length);

        using var response = await client.PostAsync(Path, Body(JsonSerializer.Serialize(report)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("TOO_LONG", problem.RootElement.GetProperty("errors").GetProperty(field)[0].GetString());
    }

    [Theory]
    [InlineData("version", 40)]
    [InlineData("route", 200)]
    [InlineData("message", 1000)]
    [InlineData("stack", 8000)]
    public async Task Field_at_its_limit_is_accepted(string field, int length)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var report = new Dictionary<string, string> { ["app"] = "clinic", ["version"] = "1", ["message"] = "m" };
        report[field] = new string('x', length);

        using var response = await client.PostAsync(Path, Body(JsonSerializer.Serialize(report)));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    public async Task Unreadable_body_is_a_malformed_request(string body)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(Path, Body(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("MALFORMED_REQUEST", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Body_over_16_KB_is_too_large()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var padding = new string(' ', 16 * 1024);

        using var response = await client.PostAsync(Path, Body(Valid() + padding));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PAYLOAD_TOO_LARGE", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_31st_request_in_a_minute_is_rate_limited()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        for (var i = 1; i <= 30; i++)
        {
            using var accepted = await client.PostAsync(Path, Body(Valid()));
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var refused = await client.PostAsync(Path, Body(Valid()));

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        Assert.Equal("RATE_LIMITED", problem.RootElement.GetProperty("code").GetString());
        Assert.Matches("^[0-9a-f]{32}$", problem.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Telemetry_endpoint_that_is_down_does_not_stop_the_platform()
    {
        using var factory = new ApiFactory().With("Scs:OTEL:ENDPOINT", "http://localhost:1");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
