using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Logging;
using Scs.IntegrationTests.Fixtures;

namespace Scs.IntegrationTests.Logging;

/// <summary>
/// SC-007: personal data and secrets are sent through every input of every mapped endpoint, on
/// success and on failure, and logged every way a developer might log them. The captured
/// output — rendered exactly as it is written to standard output — must contain none of it.
/// </summary>
[Trait("Category", "LogRedaction")]
public class LogRedactionTests
{
    [Fact]
    public async Task No_personal_data_or_secret_reaches_the_logs()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var endpoints = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
        Assert.True(endpoints.Count >= 8, "Expected the platform and probe endpoints to be mapped.");

        var sent = 0;
        foreach (var endpoint in endpoints)
        {
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"];
            foreach (var method in methods)
            {
                foreach (var body in Bodies(method))
                {
                    using var request = new HttpRequestMessage(new HttpMethod(method), LeakyAddress(endpoint));
                    request.Headers.Authorization = AuthenticationHeaderValue.Parse(Leak.Bearer);
                    request.Headers.Add("X-Patient-Mobile", Leak.MobileInternational);
                    request.Headers.Add("X-Patient-Email", Leak.Email);
                    request.Headers.Add("X-National-Id", Leak.NationalId);
                    request.Headers.Add("Cookie", $"session={Leak.Token}");

                    // The clinic header of the patient site, carrying data it should never carry.
                    request.Headers.TryAddWithoutValidation("X-Scs-Clinic", sent % 2 == 0 ? Leak.Email : Leak.NationalId);
                    request.Content = body;

                    using var response = await client.SendAsync(request);
                    sent++;
                }
            }
        }

        var output = factory.Logs.All;

        // The test is only meaningful if things were actually logged.
        Assert.True(sent >= endpoints.Count);
        Assert.Contains("\"route\":\"/probe/items/{id}\"", output, StringComparison.Ordinal);
        Assert.Contains("Unhandled failure", output, StringComparison.Ordinal);
        Assert.Contains("Structured customer", output, StringComparison.Ordinal);
        Assert.Contains("Front-end failure", output, StringComparison.Ordinal);
        Assert.Contains(ScrubbingJsonFormatter.Mask, output, StringComparison.Ordinal);

        foreach (var needle in Leak.Needles)
        {
            Assert.False(
                output.Contains(needle, StringComparison.OrdinalIgnoreCase),
                $"The logs contain '{needle}':\n{FirstLineWith(output, needle)}");
        }
    }

    [Fact]
    public async Task Classified_members_are_masked_and_other_members_kept()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/probe/log", content);

        var structured = factory.Logs.Lines.Single(l => l.Contains("Structured customer", StringComparison.Ordinal));
        Assert.Contains("\"Id\":7", structured, StringComparison.Ordinal);
        Assert.Contains("\"Plan\":\"basic\"", structured, StringComparison.Ordinal);
        Assert.Contains("\"FullName\":\"***\"", structured, StringComparison.Ordinal);
        Assert.Contains("\"Mobile\":\"***\"", structured, StringComparison.Ordinal);
        Assert.Contains("\"Token\":\"***\"", structured, StringComparison.Ordinal);

        // A domain object logged for its structure is written as its name and id.
        var patient = factory.Logs.Lines.Single(l => l.Contains("Structured patient", StringComparison.Ordinal));
        Assert.Contains("\"Patient\":\"ProbePatient#42\"", patient, StringComparison.Ordinal);

        // Any of our records logged as plain text is written as its type name alone.
        var patientText = factory.Logs.Lines.Single(l => l.Contains("Patient as text", StringComparison.Ordinal));
        Assert.Contains("\"Patient\":\"ProbePatient\"", patientText, StringComparison.Ordinal);
        var customerText = factory.Logs.Lines.Single(l => l.Contains("Customer as text", StringComparison.Ordinal));
        Assert.Contains("\"Customer\":\"ClassifiedCustomer\"", customerText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_actual_request_path_is_never_attached_to_an_entry()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var logged = await client.PostAsync("/probe/log", content);
        using var item = await client.GetAsync($"/probe/items/{Uri.EscapeDataString(Leak.Name)}");

        Assert.NotEmpty(factory.Logs.Lines);
        Assert.DoesNotContain("RequestPath", factory.Logs.All, StringComparison.Ordinal);
        Assert.DoesNotContain(Leak.NameToken, factory.Logs.All, StringComparison.Ordinal);
        Assert.Contains("\"route\":\"/probe/items/{id}\"", factory.Logs.All, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("contact mariam.zzq@example.org now", "mariam.zzq")]
    [InlineData("mobile 01012345678.", "01012345678")]
    [InlineData("mobile +201112345678", "1112345678")]
    [InlineData("mobile 00201212345678", "1212345678")]
    [InlineData("national id 30112011234567", "30112011234567")]
    [InlineData("Authorization: Bearer abcdefghijklmnop", "abcdefghijklmnop")]
    [InlineData("Server=db;User Id=scs_app;Password=Sup3rS3cretZzq;Encrypt=true", "Sup3rS3cretZzq")]
    [InlineData("Server=db;Uid=scs_app;Pwd=Sup3rS3cretZzq", "scs_app")]
    public void Scrubber_masks_pattern_shaped_data(string text, string mustDisappear)
    {
        var scrubbed = ScrubbingJsonFormatter.Scrub(text);

        Assert.DoesNotContain(mustDisappear, scrubbed, StringComparison.Ordinal);
        Assert.Contains(ScrubbingJsonFormatter.Mask, scrubbed, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"traceId":"29001011234567890123456789012345","status":200}""")]
    [InlineData("""{"@tr":"30112011234567890123456789012345","@sp":"2900101123456789"}""")]
    [InlineData("""{"@t":"2026-10-10T07:05:45.9230000Z","durationMs":12,"route":"/v1/meta/version"}""")]
    [InlineData("order 1234567 of 20 items in 2026")]
    public void Scrubber_leaves_references_and_ordinary_numbers_alone(string text)
    {
        Assert.Equal(text, ScrubbingJsonFormatter.Scrub(text));
    }

    private static IEnumerable<HttpContent?> Bodies(string method)
    {
        if (method is "GET" or "HEAD" or "DELETE")
        {
            yield return null;
            yield break;
        }

        var fields =
            $"\"name\":\"{Leak.Name}\",\"mobile\":\"{Leak.MobileInternational}\",\"email\":\"{Leak.Email}\"," +
            $"\"nationalId\":\"{Leak.NationalId}\",\"password\":\"{Leak.Password}\"";

        // Accepted by the client-errors endpoint: personal data inside free text it will log.
        yield return Json(
            $"{{\"app\":\"clinic\",\"version\":\"1.0.0\"," +
            $"\"message\":\"Failed for {Leak.Email} {Leak.MobileLocal} {Leak.NationalId} {Leak.Bearer}\"," +
            $"\"stack\":\"at save ({Leak.Connection})\"}}");

        // Rejected as invalid: unknown fields carrying personal data.
        yield return Json($"{{{fields}}}");

        // Rejected as unreadable.
        yield return Json($"{{{fields}, broken");
    }

    private static StringContent Json(string text) => new(text, Encoding.UTF8, "application/json");

    private static string LeakyAddress(RouteEndpoint endpoint)
    {
        var path = string.Concat(endpoint.RoutePattern.PathSegments.Select(segment =>
            "/" + string.Concat(segment.Parts.Select(part => part switch
            {
                Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart literal => literal.Content,
                Microsoft.AspNetCore.Routing.Patterns.RoutePatternSeparatorPart separator => separator.Content,
                // A name is the hardest case: no pattern can recognise it.
                _ => Uri.EscapeDataString($"{Leak.Name} {Leak.Email}"),
            }))));

        var query =
            $"name={Uri.EscapeDataString(Leak.Name)}&mobile={Uri.EscapeDataString(Leak.MobileInternational)}" +
            $"&email={Uri.EscapeDataString(Leak.Email)}&nid={Leak.NationalId}&token={Leak.Token}";

        return $"{path}?{query}";
    }

    private static string FirstLineWith(string output, string needle) =>
        output.Split('\n').FirstOrDefault(l => l.Contains(needle, StringComparison.OrdinalIgnoreCase)) ?? "";
}
