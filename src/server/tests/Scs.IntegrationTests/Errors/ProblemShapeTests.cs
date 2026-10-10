using System.Net;
using System.Text;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Errors;

public sealed class ProblemShapeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    // Deny-by-default also covers routes that do not exist, so a caller with no identity
    // cannot find out which routes do.
    [InlineData("/no/such/route", HttpStatusCode.Unauthorized, "UNAUTHENTICATED")]
    [InlineData("/probe/missing", HttpStatusCode.NotFound, "NOT_FOUND")]
    [InlineData("/probe/throws", HttpStatusCode.InternalServerError, "INTERNAL_ERROR")]
    [InlineData("/probe/protected", HttpStatusCode.Unauthorized, "UNAUTHENTICATED")]
    [InlineData("/probe/invalid", HttpStatusCode.BadRequest, "VALIDATION_FAILED")]
    public async Task Every_failure_has_the_uniform_shape(string path, HttpStatusCode status, string code)
    {
        var response = await _client.GetAsync(path);

        await AssertProblem(response, status, code);
    }

    [Fact]
    public async Task Unreadable_body_is_a_malformed_request()
    {
        using var content = new StringContent("{ this is not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/probe/body", content);

        await AssertProblem(response, HttpStatusCode.BadRequest, "MALFORMED_REQUEST");
    }

    [Fact]
    public async Task Validation_failure_lists_field_codes()
    {
        var response = await _client.GetAsync("/probe/invalid");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var codes = body.RootElement.GetProperty("errors").GetProperty("email");
        Assert.Equal("REQUIRED", codes[0].GetString());
    }

    [Fact]
    public async Task Unexpected_failure_reveals_no_internal_detail()
    {
        var response = await _client.GetAsync("/probe/throws");
        var text = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Secret.cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT", text, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("errors", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Incoming_trace_is_continued()
    {
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/no/such/route");
        request.Headers.Add("traceparent", $"00-{traceId}-00f067aa0ba902b7-01");

        var response = await _client.SendAsync(request);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(traceId, body.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(traceId, response.Headers.GetValues("X-Trace-Id").Single());
    }

    [Fact]
    public async Task Successful_response_also_carries_the_reference()
    {
        var response = await _client.GetAsync("/probe/open");

        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues("X-Trace-Id").Single());
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.StartsWith("urn:scs:error:", root.GetProperty("type").GetString(), StringComparison.Ordinal);

        var traceId = root.GetProperty("traceId").GetString();
        Assert.Matches("^[0-9a-f]{32}$", traceId);
        Assert.Equal(traceId, response.Headers.GetValues("X-Trace-Id").Single());
    }
}
