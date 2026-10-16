using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.FeatureFixtures;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 012, User Story 2 (FR-009 to FR-011, FR-017; contracts/features-api.md §2): a feature is
/// added without a release, at the end of its group, by whoever manages plans and nobody else;
/// every refusal saves nothing; the platform's trail records who added what.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FeaturesAddTests(SqlServerFixture sql)
{
    private static async Task<int> LastOrderAsync(SqlServerFixture sql, string group) =>
        int.Parse((await sql.OpsColumnAsync($"SELECT ISNULL(MAX(SortOrder), 0) FROM [plans].[Feature] WHERE [Group] = '{group}'")).Single(),
            System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Only_who_manages_plans_adds_a_feature()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var nobody = world.Factory.Browser();
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var body = NewSwitch();
        Assert.Equal(401, (await nobody.OpsPostAsync(Features, body).RefusalAsync()).Status);
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPostAsync(Features, body).RefusalAsync());
        Assert.Null(await world.Factory.FeatureAsync((string)body["key"]!));
    }

    [Fact]
    public async Task A_switch_lands_last_in_its_group_upcoming_sold_and_recorded()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (managerId, managerEmail, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.BillingManagePlans);
        using var manager = await world.Factory.SignInOperatorAsync(managerEmail);

        var last = await LastOrderAsync(sql, "reports");
        var body = NewSwitch(group: "reports");
        var (status, _, text) = await manager.OpsPostAsync(Features, body).AnswerAsync();

        Assert.Equal(201, status);
        var key = JsonDocument.Parse(text).RootElement.GetProperty("key").GetString()!;
        Assert.Equal(body["key"], key);

        var saved = await world.Factory.FeatureAsync(key);
        Assert.NotNull(saved);
        Assert.Equal((last + 10, "upcoming", true, false, 1), (saved.SortOrder, saved.Status, saved.IsSold, saved.IsRetired, saved.Version));

        var entry = Assert.Single(await sql.FeatureEntriesAsync(key));
        Assert.Equal(("feature.created", "operator", managerId, "قناة تجريبية"), (entry.Action, entry.ActorKind, entry.Actor, entry.TargetName));
        using var facts = JsonDocument.Parse(entry.Facts);
        Assert.Equal((key, "reports", "switch"), (
            facts.RootElement.GetProperty("feature").GetString(), facts.RootElement.GetProperty("group").GetString(),
            facts.RootElement.GetProperty("kind").GetString()));
        Assert.False(facts.RootElement.TryGetProperty("key", out _));
        Assert.Contains("Test channel", entry.Changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_limit_keeps_both_units_and_its_baseline()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var body = NewLimit(baseline: 1_000_000);
        Assert.Equal(201, (await manager.OpsPostAsync(Features, body).RefusalAsync()).Status);

        var saved = await world.Factory.FeatureAsync((string)body["key"]!);
        Assert.Equal(("limit", "رسالة/شهر", "msgs / mo", 1_000_000), (saved!.Kind, saved.UnitAr, saved.UnitEn, saved.Baseline));
    }

    public static TheoryData<string, object?, string> BadFields => new()
    {
        { "key", "Bad-Key", "key" },
        { "key", "a", "key" },
        { "key", "1abc", "key" },
        { "key", "a" + new string('b', 64), "key" },
        { "nameAr", "", "nameAr" },
        { "nameEn", new string('n', 121), "nameEn" },
        { "descriptionAr", new string('و', 501), "descriptionAr" },
        { "group", "billing", "group" },
        { "kind", "counter", "kind" },
        { "specialty", "surgery", "specialty" },
        { "status", "done", "status" },
    };

    [Theory]
    [MemberData(nameof(BadFields))]
    public async Task A_field_out_of_its_rule_is_named_and_nothing_is_saved(string field, object? value, string named)
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var body = NewSwitch().With(field, value);
        var (status, code, text) = await manager.OpsPostAsync(Features, body).AnswerAsync();

        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        Assert.True(JsonDocument.Parse(text).RootElement.GetProperty("errors").TryGetProperty(named, out _), text);
        if (body["key"] is string key && key.StartsWith("t_", StringComparison.Ordinal))
        {
            Assert.Null(await world.Factory.FeatureAsync(key));
        }
    }

    [Fact]
    public async Task Units_and_baselines_belong_to_limits_alone()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var noUnit = NewLimit().With("unitEn", " ");
        Assert.Equal((400, "FEATURE_UNITS_REQUIRED"), await manager.OpsPostAsync(Features, noUnit).RefusalAsync());

        var longUnit = NewLimit().With("unitAr", new string('و', 41));
        Assert.Equal((400, "VALIDATION_FAILED"), await manager.OpsPostAsync(Features, longUnit).RefusalAsync());

        var outOfRange = NewLimit().With("baseline", 1_000_001);
        Assert.Equal((400, "VALIDATION_FAILED"), await manager.OpsPostAsync(Features, outOfRange).RefusalAsync());

        var unitOnSwitch = NewSwitch().With("unitAr", "رسالة");
        Assert.Equal((400, "FEATURE_SWITCH_HAS_LIMIT"), await manager.OpsPostAsync(Features, unitOnSwitch).RefusalAsync());

        var baselineOnSwitch = NewSwitch().With("baseline", 0);
        Assert.Equal((400, "FEATURE_SWITCH_HAS_LIMIT"), await manager.OpsPostAsync(Features, baselineOnSwitch).RefusalAsync());

        foreach (var body in new[] { noUnit, longUnit, outOfRange, unitOnSwitch, baselineOnSwitch })
        {
            Assert.Null(await world.Factory.FeatureAsync((string)body["key"]!));
        }
    }

    [Fact]
    public async Task A_key_in_the_catalogue_is_refused_a_retired_one_too()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);
        var retired = await world.Factory.PutFeatureAsync(retired: true);

        Assert.Equal((409, "FEATURE_KEY_TAKEN"), await manager.OpsPostAsync(Features, NewSwitch("max_users")).RefusalAsync());
        Assert.Equal((409, "FEATURE_KEY_TAKEN"), await manager.OpsPostAsync(Features, NewSwitch(retired)).RefusalAsync());
        Assert.Equal("Users", (await world.Factory.FeatureAsync("max_users"))!.NameEn);
    }

    [Fact]
    public async Task Two_adds_of_one_key_at_once_save_one_and_refuse_the_other()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var first = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);
        using var second = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var key = NewKey();
        var answers = await Task.WhenAll(
            first.OpsPostAsync(Features, NewSwitch(key)).RefusalAsync(),
            second.OpsPostAsync(Features, NewSwitch(key)).RefusalAsync());

        Assert.Equal([(201, (string?)null), (409, "FEATURE_KEY_TAKEN")], answers.OrderBy(a => a.Status));
        Assert.Single(await sql.FeatureEntriesAsync(key));
    }

    [Fact]
    public async Task Two_adds_to_one_group_at_once_take_two_orders()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var first = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);
        using var second = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var (a, b) = (NewSwitch(group: "ai"), NewSwitch(group: "ai"));
        var answers = await Task.WhenAll(first.OpsPostAsync(Features, a).RefusalAsync(), second.OpsPostAsync(Features, b).RefusalAsync());
        Assert.All(answers, answer => Assert.Equal(201, answer.Status));

        var orders = new[] { (await world.Factory.FeatureAsync((string)a["key"]!))!.SortOrder, (await world.Factory.FeatureAsync((string)b["key"]!))!.SortOrder };
        Assert.Equal(10, Math.Abs(orders[0] - orders[1]));
    }
}
