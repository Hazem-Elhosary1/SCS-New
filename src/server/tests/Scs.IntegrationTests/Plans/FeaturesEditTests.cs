using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.FeatureFixtures;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 012, User Story 3 (FR-009 to FR-011, FR-017; contracts/features-api.md §2): everything of a
/// feature but its key is corrected; a move puts it last in the new group; an edit made on an
/// older reading is refused; one open across a retire still saves.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FeaturesEditTests(SqlServerFixture sql)
{
    private static async Task<Dictionary<string, object?>> BodyAsync(HttpClient browser, string key)
    {
        var row = await browser.JsonAsync($"{Features}/{key}");
        object? Value(string name) => row.GetProperty(name).ValueKind switch
        {
            JsonValueKind.String => row.GetProperty(name).GetString(),
            JsonValueKind.Number => row.GetProperty(name).GetInt32(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["nameAr"] = Value("nameAr"), ["nameEn"] = Value("nameEn"), ["descriptionAr"] = Value("descriptionAr"),
            ["descriptionEn"] = Value("descriptionEn"), ["group"] = Value("group"), ["kind"] = Value("kind"), ["unitAr"] = Value("unitAr"),
            ["unitEn"] = Value("unitEn"), ["baseline"] = Value("baseline"), ["specialty"] = Value("specialty"), ["status"] = Value("status"),
            ["isSold"] = Value("isSold"), ["version"] = Value("version"),
        };
    }

    [Fact]
    public async Task Only_who_manages_plans_edits_a_feature()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var key = await world.Factory.PutFeatureAsync();
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);
        using var nobody = world.Factory.Browser();

        var body = (await BodyAsync(viewer, key)).With("nameEn", "Changed");
        Assert.Equal(401, (await nobody.OpsPutAsync($"{Features}/{key}", body).RefusalAsync()).Status);
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPutAsync($"{Features}/{key}", body).RefusalAsync());
        Assert.Equal("Test feature", (await world.Factory.FeatureAsync(key))!.NameEn);
    }

    [Fact]
    public async Task A_change_is_saved_its_version_raised_and_only_what_changed_recorded()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var key = await world.Factory.PutFeatureAsync();
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var body = (await BodyAsync(manager, key)).With("nameEn", "Renamed").With("status", "built");
        Assert.Equal(204, (await manager.OpsPutAsync($"{Features}/{key}", body).RefusalAsync()).Status);

        var saved = await world.Factory.FeatureAsync(key);
        Assert.Equal(("Renamed", "built", 2), (saved!.NameEn, saved.Status, saved.Version));

        var entry = Assert.Single(await sql.FeatureEntriesAsync(key, "feature.updated"));
        using var changes = JsonDocument.Parse(entry.Changes);
        Assert.Equal(["nameEn", "status"], changes.RootElement.EnumerateArray().Select(c => c.GetProperty("field").GetString()).Order(StringComparer.Ordinal));
        Assert.Contains("Test feature", entry.Changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_key_is_the_address_s_and_one_in_the_body_is_never_read()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var key = await world.Factory.PutFeatureAsync();
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var other = NewKey();
        var body = (await BodyAsync(manager, key)).With("key", other).With("nameEn", "Same key");
        Assert.Equal(204, (await manager.OpsPutAsync($"{Features}/{key}", body).RefusalAsync()).Status);

        Assert.Equal("Same key", (await world.Factory.FeatureAsync(key))!.NameEn);
        Assert.Null(await world.Factory.FeatureAsync(other));
    }

    [Fact]
    public async Task A_feature_moved_to_another_group_goes_last_there()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var key = await world.Factory.PutFeatureAsync(group: "channels", sortOrder: 15);
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var last = int.Parse(
            (await sql.OpsColumnAsync("SELECT ISNULL(MAX(SortOrder), 0) FROM [plans].[Feature] WHERE [Group] = 'reports'")).Single(),
            System.Globalization.CultureInfo.InvariantCulture);
        var body = (await BodyAsync(manager, key)).With("group", "reports");
        Assert.Equal(204, (await manager.OpsPutAsync($"{Features}/{key}", body).RefusalAsync()).Status);

        var saved = await world.Factory.FeatureAsync(key);
        Assert.Equal(("reports", last + 10), (saved!.Group, saved.SortOrder));

        var same = (await BodyAsync(manager, key)).With("nameAr", "اسم آخر");
        Assert.Equal(204, (await manager.OpsPutAsync($"{Features}/{key}", same).RefusalAsync()).Status);
        Assert.Equal(last + 10, (await world.Factory.FeatureAsync(key))!.SortOrder);
    }

    [Fact]
    public async Task A_switch_and_a_limit_trade_places_only_with_their_units_right()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var limit = await world.Factory.PutFeatureAsync(kind: "limit");
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var keepsUnits = (await BodyAsync(manager, limit)).With("kind", "switch");
        Assert.Equal((400, "FEATURE_SWITCH_HAS_LIMIT"), await manager.OpsPutAsync($"{Features}/{limit}", keepsUnits).RefusalAsync());

        // A refused shape saves nothing and records nothing (US3 scenario 4).
        var untouched = await world.Factory.FeatureAsync(limit);
        Assert.Equal(("limit", "وحدة", "units", 0, 1), (untouched!.Kind, untouched.UnitAr, untouched.UnitEn, untouched.Baseline, untouched.Version));
        Assert.Empty(await sql.FeatureEntriesAsync(limit, "feature.updated"));

        var cleared = keepsUnits.With("unitAr", null).With("unitEn", null).With("baseline", null);
        Assert.Equal(204, (await manager.OpsPutAsync($"{Features}/{limit}", cleared).RefusalAsync()).Status);
        Assert.Equal(("switch", (string?)null, (int?)null), ((await world.Factory.FeatureAsync(limit))!.Kind, (await world.Factory.FeatureAsync(limit))!.UnitAr, (await world.Factory.FeatureAsync(limit))!.Baseline));

        var noUnits = (await BodyAsync(manager, limit)).With("kind", "limit");
        Assert.Equal((400, "FEATURE_UNITS_REQUIRED"), await manager.OpsPutAsync($"{Features}/{limit}", noUnits).RefusalAsync());
        var stillSwitch = await world.Factory.FeatureAsync(limit);
        Assert.Equal(("switch", 2), (stillSwitch!.Kind, stillSwitch.Version));
        Assert.Single(await sql.FeatureEntriesAsync(limit, "feature.updated"));
    }

    [Fact]
    public async Task An_edit_on_an_older_reading_is_refused_and_saves_nothing()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var key = await world.Factory.PutFeatureAsync();
        using var first = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);
        using var second = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var one = (await BodyAsync(first, key)).With("nameEn", "First");
        var two = (await BodyAsync(second, key)).With("nameEn", "Second");

        Assert.Equal(204, (await first.OpsPutAsync($"{Features}/{key}", one).RefusalAsync()).Status);
        Assert.Equal((409, "FEATURE_CHANGED_ELSEWHERE"), await second.OpsPutAsync($"{Features}/{key}", two).RefusalAsync());
        Assert.Equal("First", (await world.Factory.FeatureAsync(key))!.NameEn);

        Assert.Equal((400, "VALIDATION_FAILED"), await first.OpsPutAsync($"{Features}/{key}", one.With("version", null)).RefusalAsync());
    }

    [Fact]
    public async Task An_edit_open_across_a_retire_saves_its_fields_and_the_feature_stays_retired()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var key = await world.Factory.PutFeatureAsync();
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var body = (await BodyAsync(manager, key)).With("nameEn", "Edited while retired");
        Assert.Equal(204, (await manager.OpsPostAsync($"{Features}/{key}/retire").RefusalAsync()).Status);
        Assert.Equal(204, (await manager.OpsPutAsync($"{Features}/{key}", body).RefusalAsync()).Status);

        var saved = await world.Factory.FeatureAsync(key);
        Assert.Equal(("Edited while retired", true), (saved!.NameEn, saved.IsRetired));
    }

    [Fact]
    public async Task Sold_is_not_turned_off_a_retired_feature_and_an_unknown_key_is_not_found()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var key = await world.Factory.PutFeatureAsync(retired: true);
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var body = (await BodyAsync(manager, key)).With("isSold", false);
        Assert.Equal((409, "FEATURE_RETIRED_MUST_BE_SOLD"), await manager.OpsPutAsync($"{Features}/{key}", body).RefusalAsync());
        Assert.True((await world.Factory.FeatureAsync(key))!.IsSold);

        Assert.Equal((404, "NOT_FOUND"), await manager.OpsPutAsync($"{Features}/{NewKey()}", body.With("isSold", true)).RefusalAsync());
    }
}
