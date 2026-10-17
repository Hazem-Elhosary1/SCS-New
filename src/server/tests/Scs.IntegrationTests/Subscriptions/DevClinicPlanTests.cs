using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Security;
using Scs.IntegrationTests.Fixtures;
using Scs.Plans.Contracts;

namespace Scs.IntegrationTests.Subscriptions;

/// <summary>
/// Spec 014, FR-031 (research R15): a seeded clinic is put on a plan on a developer's machine, and
/// the route does not exist anywhere else.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class DevClinicPlanTests(SqlServerFixture sql)
{
    [Fact]
    public async Task On_a_developers_machine_a_seeded_clinic_is_put_on_an_active_plan_and_taken_off_it()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var plan = await factory.PutPlanAsync();
        var code = (await factory.PlanAsync(plan))!.Code;
        var clinic = await sql.Main.SeedClinicAsync();
        var name = $"walk-{Guid.NewGuid():N}"[..20];
        await sql.ExecuteAsync(
            sql.Main.Crossing,
            $"INSERT INTO tenancy.ClinicDirectory (ShortName, ClinicId, IsCurrent, AssignedAtUtc) VALUES ('{name}', '{clinic}', 1, SYSDATETIMEOFFSET())");
        using var browser = factory.Browser();

        using var unknownClinic = await browser.PostAsJsonAsync("/v1/dev/clinic-plan", new { clinic = "nosuch", planCode = code });
        using var unknownPlan = await browser.PostAsJsonAsync("/v1/dev/clinic-plan", new { clinic = name, planCode = "no-such-plan" });
        using var set = await browser.PostAsJsonAsync("/v1/dev/clinic-plan", new { clinic = name, planCode = code });
        using var none = await browser.PostAsJsonAsync("/v1/dev/clinic-plan", new { clinic = name, planCode = (string?)null });

        Assert.Equal(HttpStatusCode.NotFound, unknownClinic.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownPlan.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);
        Assert.Equal(2, await TestDatabase.ScalarAsync<int>(
            sql.AdminConnection, $"SELECT COUNT(*) FROM [audit].[PlatformEntry] WHERE ActionCode = 'subscription.plan-set' AND TargetId = '{clinic}'"));
    }

    [Theory]
    [InlineData("test")]
    [InlineData("production")]
    public async Task Anywhere_else_the_route_does_not_exist(string environment)
    {
        using var deployed = new ApiFactory().With("Scs:ENVIRONMENT", environment);
        using var client = deployed.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(RequestGuardExtensions.RequestHeader, "1");

        using var missing = await client.PostAsJsonAsync("/v1/dev/clinic-plan", new { clinic = "noor", planCode = "any" });

        Assert.True(missing.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized, $"{environment}: {(int)missing.StatusCode}");
        Assert.DoesNotContain(
            deployed.Services.GetRequiredService<EndpointDataSource>().Endpoints,
            e => e.DisplayName?.Contains("DevSetClinicPlan", StringComparison.Ordinal) == true);
    }
}
