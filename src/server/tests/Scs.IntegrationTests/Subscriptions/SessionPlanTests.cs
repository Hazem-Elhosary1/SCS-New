using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Plans;

namespace Scs.IntegrationTests.Subscriptions;

/// <summary>
/// Spec 014, FR-025 and FR-029 (contracts/enforcement-api.md §6): the session answer tells the
/// clinic console what its plan leaves out — only built switches, with their names — and nothing
/// of other plans or clinics.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SessionPlanTests(SqlServerFixture sql)
{
    private static async Task<JsonElement> PlanOfAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync(new Uri("/v1/auth/session", UriKind.Relative));
        Assert.True(response.IsSuccessStatusCode, $"The session answered {(int)response.StatusCode}.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("plan").Clone();
    }

    private static string[] Withheld(JsonElement plan) =>
        [.. plan.GetProperty("withheld").EnumerateArray().Select(k => k.GetString()!)];

    // Other tests leave features of their own built in the shared database, so each test speaks only
    // of the features it marks.
    [Fact]
    public async Task An_upcoming_feature_is_never_withheld()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        await using var upcoming = await sql.MarkAsync("upcoming", FeatureKeys.AllowAnalytics, FeatureKeys.AllowWhatsapp);
        using var browser = await factory.SignInAsAsync(clinic);

        var plan = await PlanOfAsync(browser);

        Assert.DoesNotContain("allow_analytics", Withheld(plan));
        Assert.DoesNotContain("allow_whatsapp", Withheld(plan));
        Assert.DoesNotContain("max_users", Withheld(plan));
        Assert.Equal(JsonValueKind.Null, plan.GetProperty("nameAr").ValueKind);
    }

    [Fact]
    public async Task The_built_switches_the_plan_leaves_out_are_listed_with_their_names()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        await using var built = await sql.BuiltAsync(FeatureKeys.AllowAnalytics, FeatureKeys.AllowWhatsapp);
        await factory.GivePlanAsync(clinic, (FeatureKeys.AllowWhatsapp, null), (FeatureKeys.MaxUsers, null));
        using var browser = await factory.SignInAsAsync(clinic);

        var plan = await PlanOfAsync(browser);

        Assert.Contains("allow_analytics", Withheld(plan));
        Assert.DoesNotContain("allow_whatsapp", Withheld(plan));
        Assert.Equal("باقة تجريبية", plan.GetProperty("nameAr").GetString());
        Assert.Equal("التقارير المالية والطبية", plan.GetProperty("names").GetProperty("allow_analytics").GetProperty("ar").GetString());
    }

    [Fact]
    public async Task A_plan_changed_shows_in_the_next_session_answer()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        await using var built = await sql.BuiltAsync(FeatureKeys.AllowAnalytics);
        using var browser = await factory.SignInAsAsync(clinic);
        Assert.Contains("allow_analytics", Withheld(await PlanOfAsync(browser)));

        await factory.GivePlanAsync(clinic, (FeatureKeys.AllowAnalytics, null), (FeatureKeys.MaxUsers, null));

        Assert.DoesNotContain("allow_analytics", Withheld(await PlanOfAsync(browser)));
    }
}
