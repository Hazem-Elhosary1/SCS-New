using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Plans;
using Scs.BuildingBlocks.Security;
using Scs.IntegrationTests.Fixtures;
using Scs.Plans.Infrastructure.Seed;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Plans;

namespace Scs.IntegrationTests.Subscriptions;

/// <summary>
/// Spec 014, User Story 2 (FR-008 to FR-010; contracts/enforcement-api.md §3): a route that
/// belongs to a feature is refused, before its handler runs, for a clinic whose plan does not
/// include it — once the feature is built — with one code and one message; the permission is
/// decided first; an upcoming feature is refused for no one. Test-first: plan entitlements.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlanGateTests(SqlServerFixture sql)
{
    private const string Analytics = "/probe/plan/analytics";
    private const string Channels = "/probe/plan/channels";

    private static async Task<(int Status, string? Code, string? Feature, string? Title)> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            return ((int)response.StatusCode, null, null, null);
        }

        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        return (
            (int)response.StatusCode,
            root.GetProperty("code").GetString(),
            root.TryGetProperty("feature", out var feature) ? feature.GetString() : null,
            root.GetProperty("title").GetString());
    }

    private static async Task<(int Status, string? Code, string? Feature, string? Title)> GetAsync(
        HttpClient browser, string path, string language = "ar")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.AcceptLanguage.ParseAdd(language);
        using var response = await browser.SendAsync(request);
        return await ReadAsync(response);
    }

    [Fact]
    public async Task A_built_feature_the_plan_leaves_out_is_refused_with_one_message_and_the_handler_never_runs()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        await using var built = await sql.BuiltAsync(FeatureKeys.AllowAnalytics);
        await factory.GivePlanAsync(clinic, (FeatureKeys.MaxUsers, null));
        using var person = await factory.SignInWithAsync(clinic, Permissions.PatientsView, Permissions.PatientsManage);
        var ranBefore = ProbeEndpoints.PlanRuns.GetValueOrDefault("analytics");

        Assert.Equal((403, "PLAN_FEATURE_NOT_INCLUDED", "allow_analytics", "غير مشمولة في باقتك"), await GetAsync(person.Browser, Analytics));
        Assert.Equal("Not included in your plan", (await GetAsync(person.Browser, Analytics, "en")).Title);
        Assert.Equal(ranBefore, ProbeEndpoints.PlanRuns.GetValueOrDefault("analytics"));
    }

    [Fact]
    public async Task A_plan_that_includes_the_feature_lets_the_route_run()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        await using var built = await sql.BuiltAsync(FeatureKeys.AllowAnalytics);
        await factory.GivePlanAsync(clinic, (FeatureKeys.AllowAnalytics, null));
        using var person = await factory.SignInWithAsync(clinic, Permissions.PatientsView, Permissions.PatientsManage);

        Assert.Equal(200, (await GetAsync(person.Browser, Analytics)).Status);
    }

    [Fact]
    public async Task Who_may_not_call_the_route_is_told_so_and_never_about_the_plan()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        await using var built = await sql.BuiltAsync(FeatureKeys.AllowAnalytics);
        using var person = await factory.SignInWithAsync(clinic, Permissions.PatientsView);

        var answer = await GetAsync(person.Browser, Analytics);

        Assert.Equal((403, "PERMISSION_REQUIRED"), (answer.Status, answer.Code));
    }

    [Fact]
    public async Task An_upcoming_feature_is_refused_for_no_one()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        await using var upcoming = await sql.MarkAsync("upcoming", FeatureKeys.AllowAnalytics);
        using var person = await factory.SignInWithAsync(clinic, Permissions.PatientsView, Permissions.PatientsManage);

        Assert.Equal(200, (await GetAsync(person.Browser, Analytics)).Status);
    }

    [Fact]
    public async Task A_route_of_any_of_two_features_needs_only_one_of_them()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var withOne = await sql.Main.SeedClinicAsync();
        var withNone = await sql.Main.SeedClinicAsync();
        await using var built = await sql.BuiltAsync(FeatureKeys.AllowWhatsapp, FeatureKeys.AllowTelegram);
        await factory.GivePlanAsync(withOne, (FeatureKeys.AllowTelegram, null));
        await factory.GivePlanAsync(withNone, (FeatureKeys.MaxUsers, null));
        using var one = await factory.SignInWithAsync(withOne, Permissions.PatientsView, Permissions.PatientsManage);
        using var none = await factory.SignInWithAsync(withNone, Permissions.PatientsView, Permissions.PatientsManage);

        Assert.Equal(200, (await GetAsync(one.Browser, Channels)).Status);
        Assert.Equal("PLAN_FEATURE_NOT_INCLUDED", (await GetAsync(none.Browser, Channels)).Code);
    }

    [Fact]
    public async Task Every_feature_a_route_names_is_in_the_catalogue_and_no_operators_route_names_one()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var seeded = FeatureSeed.Features().Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        var named = routes.Where(e => e.Metadata.GetMetadata<PlanFeatureMetadata>() is not null).ToList();
        Assert.NotEmpty(named);

        var strangers = named
            .SelectMany(e => e.Metadata.GetMetadata<PlanFeatureMetadata>()!.Keys.Select(k => (e.RoutePattern.RawText, Key: k)))
            .Where(x => !seeded.Contains(x.Key))
            .ToList();
        Assert.True(strangers.Count == 0, "Not a feature of the catalogue: " + string.Join(", ", strangers));

        var operators = named.Where(e => e.Metadata.GetMetadata<OperatorRouteMetadata>() is not null).Select(e => e.RoutePattern.RawText).ToList();
        Assert.True(operators.Count == 0, "An operators' route names a plan feature: " + string.Join(", ", operators));
    }

    [Fact]
    public void A_route_names_its_feature_once()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        app.MapGet("/twice", () => Results.Ok()).RequiresFeature(FeatureKeys.AllowAnalytics).RequiresFeature("allow_insurance");

        var failure = Assert.Throws<InvalidOperationException>(() => ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).ToList());
        Assert.Contains("more than once", failure.Message, StringComparison.Ordinal);
    }
}
