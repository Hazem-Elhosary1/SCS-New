using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.Plans.Contracts;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.FeatureFixtures;
using static Scs.IntegrationTests.Fixtures.PlanFixtures;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 013, User Story 6 (FR-027; contracts/plans-api.md §3): the catalogue of spec 012 counts the
/// plans that include each feature and lists them, with their state and clinics, in «أين تُستعمل».
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FeaturePlansCountTests(SqlServerFixture sql)
{
    private static JsonElement Row(JsonElement page, string key) =>
        page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == key);

    [Fact]
    public async Task Each_feature_counts_the_plans_that_include_it_and_the_cards_count_them_all()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var twice = await world.Factory.PutFeatureAsync();
        var once = await world.Factory.PutFeatureAsync();
        var never = await world.Factory.PutFeatureAsync();
        await world.Factory.PutPlanAsync(grid: [(twice, null), (once, null)]);
        await world.Factory.PutPlanAsync(grid: (twice, null));
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var page = await viewer.JsonAsync(Features);
        Assert.Equal(2, Row(page, twice).GetProperty("plans").GetInt32());
        Assert.Equal(1, Row(page, once).GetProperty("plans").GetInt32());
        Assert.Equal(0, Row(page, never).GetProperty("plans").GetInt32());

        var items = page.GetProperty("items").EnumerateArray().ToList();
        var cards = page.GetProperty("cards");
        Assert.Equal(items.Count(i => i.GetProperty("plans").GetInt32() > 0), cards.GetProperty("soldInPlans").GetInt32());
        var plans = await TestDatabase.ReadColumnAsync(sql.Main.Admin, "SELECT CAST(COUNT(DISTINCT [PlanId]) AS nvarchar(10)) FROM [plans].[PlanFeature]");
        Assert.Equal(int.Parse(plans.Single(), System.Globalization.CultureInfo.InvariantCulture), cards.GetProperty("plans").GetInt32());
    }

    [Fact]
    public async Task Where_it_is_used_lists_each_plan_with_its_state_and_clinics()
    {
        var clinics = new FakeSubscribers();
        using var world = await OpsWorld.CreateAsync(sql, configure: f => f.WithSubscribers(clinics));
        var feature = await world.Factory.PutFeatureAsync();
        var active = await world.Factory.PutPlanAsync(grid: (feature, null));
        var disabled = await world.Factory.PutPlanAsync(disabled: true, grid: (feature, null));
        clinics.Counts[active] = new PlanSubscriberCounts(64, 0, 0, 0m);
        clinics.Counts[disabled] = new PlanSubscriberCounts(12, 0, 0, 0m);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var used = await viewer.JsonAsync($"{Features}/{feature}/plans");
        var items = used.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetGuid(), i => i);
        Assert.Equal(2, items.Count);
        Assert.Equal("active", items[active].GetProperty("state").GetString());
        Assert.Equal("disabled", items[disabled].GetProperty("state").GetString());
        Assert.Equal(64, items[active].GetProperty("clinics").GetInt32());
        Assert.Equal("باقة تجريبية", items[active].GetProperty("nameAr").GetString());
        Assert.True(items[active].TryGetProperty("nameEn", out _));
        Assert.Equal(76, used.GetProperty("total").GetInt32());
    }
}
