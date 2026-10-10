using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Tenancy;
using Scs.IntegrationTests.Fixtures;
using Scs.Tenancy.Contracts;

namespace Scs.IntegrationTests.Tenancy;

/// <summary>
/// Spec 002, User Story 3, against contracts/tenancy-api.openapi.yaml: a patient address leads
/// to its clinic, and everything else gets one identical "not available" answer.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClinicEndpointTests(SqlServerFixture sql)
{
    private const string Path = "/v1/clinic";
    private const string Operator = "test:endpoint";

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    private async Task<(ApiFactory Factory, HttpClient Client)> StartAsync()
    {
        await sql.EnsureMigratedAsync();
        var factory = sql.CreateApi();
        return (factory, factory.CreateClient());
    }

    private static async Task<Guid> RegisterAsync(ApiFactory factory, string name, string nameAr = "عيادة النور", string nameEn = "Al Noor Clinic")
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IClinicRegistry>().RegisterAsync(name, nameAr, nameEn, Operator, default);
    }

    private static async Task<HttpResponseMessage> AskAsync(HttpClient client, string? clinic)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);
        if (clinic is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Scs-Clinic", clinic);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task A_known_name_answers_with_the_clinic()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;
        var name = Unique("ep");
        await RegisterAsync(factory, name);

        using var response = await AskAsync(client, name);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues("X-Trace-Id").Single());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(name, body.RootElement.GetProperty("shortName").GetString());
        Assert.Equal("عيادة النور", body.RootElement.GetProperty("nameAr").GetString());
        Assert.Equal("Al Noor Clinic", body.RootElement.GetProperty("nameEn").GetString());
        Assert.Equal("active", body.RootElement.GetProperty("state").GetString());
        Assert.False(body.RootElement.GetProperty("renamed").GetBoolean());
    }

    [Fact]
    public async Task Letter_case_spaces_and_slashes_are_ignored()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;
        var name = Unique("norm");
        await RegisterAsync(factory, name);

        foreach (var variant in new[] { name.ToUpperInvariant(), $"  {name}  ", $"/{name}/" })
        {
            using var response = await AskAsync(client, variant);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(name, body.RootElement.GetProperty("shortName").GetString());
        }
    }

    [Fact]
    public async Task An_earlier_name_answers_with_the_current_one_and_says_it_was_renamed()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;
        var old = Unique("was");
        var current = Unique("now");
        var id = await RegisterAsync(factory, old);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IClinicRegistry>().RenameAsync(id, current, Operator, default);
        }

        using var response = await AskAsync(client, old);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(current, body.RootElement.GetProperty("shortName").GetString());
        Assert.True(body.RootElement.GetProperty("renamed").GetBoolean());
    }

    [Fact]
    public async Task Every_address_that_leads_nowhere_gets_the_same_answer()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;
        var suspended = Unique("susp");
        var cancelled = Unique("canc");
        var suspendedId = await RegisterAsync(factory, suspended, nameEn: "Hidden Suspended Clinic");
        var cancelledId = await RegisterAsync(factory, cancelled, nameEn: "Hidden Cancelled Clinic");
        await TestDatabase.ExecuteAsync(sql.Main.Crossing, $"UPDATE tenancy.Clinic SET State = 'suspended' WHERE Id = '{suspendedId}'");
        await TestDatabase.ExecuteAsync(sql.Main.Crossing, $"UPDATE tenancy.Clinic SET State = 'cancelled' WHERE Id = '{cancelledId}'");

        var bodies = new List<string>();
        foreach (var name in new[] { Unique("ghost"), Unique("phantom"), "api", "ab", "النور", "no such clinic", new string('a', 61), suspended, cancelled })
        {
            using var response = await AskAsync(client, name);
            var text = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            using var body = JsonDocument.Parse(text);
            Assert.Equal("CLINIC_NOT_AVAILABLE", body.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain("Hidden", text, StringComparison.Ordinal);

            // Identical apart from the request reference.
            bodies.Add(text.Replace(body.RootElement.GetProperty("traceId").GetString()!, "", StringComparison.Ordinal));
        }

        Assert.Single(bodies.Distinct());
    }

    [Fact]
    public async Task Log_entries_carry_the_clinic_id_and_nothing_else_about_the_clinic()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;
        var first = Unique("logold");
        var current = Unique("lognew");
        const string nameAr = "عيادة السرّ المكتوم";
        const string nameEn = "Zzq Unlogged Clinic";
        var id = await RegisterAsync(factory, first, nameAr, nameEn);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IClinicRegistry>().RenameAsync(id, current, Operator, default);
        }

        using var byCurrent = await AskAsync(client, current);
        using var byEarlier = await AskAsync(client, $" /{first.ToUpperInvariant()}/ ");
        using var unknown = await AskAsync(client, Unique("lognone"));
        Assert.Equal(HttpStatusCode.OK, byCurrent.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byEarlier.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var logs = factory.Logs.All;
        var forClinic = factory.Logs.Lines.Where(l => l.Contains($"\"clinicId\":\"{id}\"", StringComparison.OrdinalIgnoreCase)).ToList();

        // The work done for the clinic is marked with its id (FR-031)…
        Assert.Contains(forClinic, l => l.Contains("\"route\":\"/v1/clinic\"", StringComparison.Ordinal));

        // …and nothing else about the clinic is written anywhere: no name it has or had.
        foreach (var secret in new[] { first, current, first.ToUpperInvariant(), nameAr, "المكتوم", nameEn, "Unlogged" })
        {
            Assert.False(
                logs.Contains(secret, StringComparison.OrdinalIgnoreCase),
                $"The logs contain '{secret}':\n{logs.Split('\n').FirstOrDefault(l => l.Contains(secret, StringComparison.OrdinalIgnoreCase))}");
        }

        foreach (var property in new[] { "shortName", "nameAr", "nameEn", "clinicName", "X-Scs-Clinic" })
        {
            Assert.DoesNotContain($"\"{property}\"", logs, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_known_and_an_unknown_address_are_both_answered_well_inside_half_a_second()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;
        var name = Unique("fast");
        await RegisterAsync(factory, name);
        for (var i = 0; i < 5; i++)
        {
            using var warm = await AskAsync(client, name);
        }

        async Task<double> MeanMsAsync(Func<string> clinic)
        {
            const int requests = 40;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < requests; i++)
            {
                using var response = await AskAsync(client, clinic());
            }

            return watch.Elapsed.TotalMilliseconds / requests;
        }

        var known = await MeanMsAsync(() => name);
        var unknownName = await MeanMsAsync(() => Unique("slow"));

        // SC-006 gives the patient half a second in all; the platform's share must leave room
        // for the network and the page.
        Assert.True(known < 100, $"A known clinic took {known:F1} ms on average.");
        Assert.True(unknownName < 100, $"An unknown clinic took {unknownName:F1} ms on average.");
    }

    [Fact]
    public async Task Without_the_header_the_request_is_refused()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        using var response = await AskAsync(client, clinic: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CLINIC_REQUIRED", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_header_is_ignored_on_endpoints_that_are_not_patient_surface()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;
        var name = Unique("staff");
        var id = await RegisterAsync(factory, name);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/clinic");
        request.Headers.Add("X-Scs-Clinic", name);
        using var response = await client.SendAsync(request);

        // The probe reports which clinic, if any, the work was established for.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(id.ToString(), await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task On_a_patient_surface_endpoint_the_header_establishes_the_clinic_and_only_that_clinic()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;
        var name = Unique("pat");
        var id = await RegisterAsync(factory, name);
        await RegisterAsync(factory, Unique("else"));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/patient-clinic");
        request.Headers.Add("X-Scs-Clinic", name);
        using var response = await client.SendAsync(request);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, body.RootElement.GetProperty("clinicId").GetGuid());
        Assert.Equal(1, body.RootElement.GetProperty("clinicsVisible").GetInt32());

        var entry = factory.Logs.Lines.Last(l => l.Contains("\"route\":\"/probe/patient-clinic\"", StringComparison.Ordinal));
        Assert.Contains($"\"clinicId\":\"{id}\"", entry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(name, entry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Work_already_established_for_one_clinic_is_refused_when_the_header_names_another()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        // Stands in for a signed-in session (spec 070): the work has a clinic before the header is read.
        using var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(new AlreadyEstablished())))
            .CreateClient();
        var name = Unique("one");
        await RegisterAsync(factory, name);
        var other = await RegisterAsync(factory, Unique("two"));

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/probe/patient-clinic?already={other}");
        request.Headers.Add("X-Scs-Clinic", name);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CLINIC_MISMATCH", body.RootElement.GetProperty("code").GetString());
    }

    private sealed class AlreadyEstablished : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                if (Guid.TryParse(context.Request.Query["already"], out var clinicId))
                {
                    context.RequestServices.GetRequiredService<IClinicContext>().Establish(clinicId, ClinicSource.Account);
                }

                return following(context);
            });
            next(app);
        };
    }

    [Fact]
    public async Task Looking_up_names_is_rate_limited()
    {
        var (factory, client) = await StartAsync();
        using var _ = factory;

        HttpStatusCode last = default;
        for (var i = 0; i < 121; i++)
        {
            using var response = await AskAsync(client, "nobody-here");
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }
}
