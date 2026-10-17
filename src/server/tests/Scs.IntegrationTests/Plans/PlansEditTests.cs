using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.FeatureFixtures;
using static Scs.IntegrationTests.Fixtures.PlanFixtures;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 013, User Story 3 (FR-015, FR-017 to FR-020, FR-024; contracts/plans-api.md §2): a plan is
/// corrected, never its code; a save replaces its grid exactly; what it already had of a retired
/// or unsold feature it keeps (ق-٤٨); an edit based on an older plan is refused.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlansEditTests(SqlServerFixture sql)
{
    private static string[] Manager { get; } = [OperatorPermissions.BillingView, OperatorPermissions.BillingManagePlans];

    private static Dictionary<string, object?> Edit(string nameEn, int version, params object[] features) =>
        NewPlan(nameEn, features).With("version", version);

    [Fact]
    public async Task Only_who_manages_plans_edits_and_reads_the_edit_grid()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var id = await world.Factory.PutPlanAsync();
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        Assert.Equal(200, (await viewer.OpsGetAsync($"{PlansPath}/{id}/grid").RefusalAsync()).Status);
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPutAsync($"{PlansPath}/{id}", Edit(NewName(), 1)).RefusalAsync());
    }

    [Fact]
    public async Task A_save_replaces_the_grid_exactly_and_never_changes_the_code()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var gone = await world.Factory.PutFeatureAsync();
        var stays = await world.Factory.PutFeatureAsync(kind: "limit");
        var added = await world.Factory.PutFeatureAsync();
        var name = NewName("First Name");
        var id = await world.Factory.PutPlanAsync(name, grid: [(gone, null), (stays, 5)]);
        var code = (await world.Factory.PlanAsync(id))!.Code;
        using var manager = await world.Factory.SignInWithAsync(Manager);

        Assert.Equal(204, (await manager.OpsPutAsync($"{PlansPath}/{id}", Edit(NewName("Another Name"), 1, Row(stays, 9), Row(added))).RefusalAsync()).Status);

        var plan = await world.Factory.PlanAsync(id);
        Assert.Equal(code, plan!.Code);
        Assert.Equal(2, plan.Version);
        Assert.Equal(2, plan.Features.Count);
        Assert.False(plan.Features.ContainsKey(gone));
        Assert.Equal(9, plan.Features[stays]);
        Assert.Null(plan.Features[added]);
    }

    [Fact]
    public async Task The_edit_grid_keeps_what_the_plan_has_of_a_retired_or_unsold_feature()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var retired = await world.Factory.PutFeatureAsync();
        var unsold = await world.Factory.PutFeatureAsync();
        var id = await world.Factory.PutPlanAsync(grid: [(retired, null), (unsold, null)]);
        await TestDatabase.ExecuteAsync(sql.Main.Admin, $"UPDATE [plans].[Feature] SET [IsRetired] = 1 WHERE [Key] = '{retired}'");
        await TestDatabase.ExecuteAsync(sql.Main.Admin, $"UPDATE [plans].[Feature] SET [IsSold] = 0 WHERE [Key] = '{unsold}'");
        using var manager = await world.Factory.SignInWithAsync(Manager);

        var grid = await manager.JsonAsync($"{PlansPath}/{id}/grid");
        var rows = grid.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("key").GetString()!, i => i);
        Assert.True(rows[retired].GetProperty("retired").GetBoolean());
        Assert.False(rows[unsold].GetProperty("sold").GetBoolean());
        Assert.DoesNotContain(retired, (await manager.JsonAsync($"{PlansPath}/grid")).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()));

        // Kept when sent again …
        Assert.Equal(204, (await manager.OpsPutAsync($"{PlansPath}/{id}", Edit(NewName(), 1, Row(retired), Row(unsold))).RefusalAsync()).Status);
        Assert.Equal(2, (await world.Factory.PlanAsync(id))!.Features.Count);

        // … and another plan cannot take them.
        var other = await world.Factory.PutPlanAsync();
        Assert.Equal((409, "PLAN_FEATURE_NOT_SOLD"), await manager.OpsPutAsync($"{PlansPath}/{other}", Edit(NewName(), 1, Row(retired))).RefusalAsync());
        Assert.Equal((409, "PLAN_FEATURE_NOT_SOLD"), await manager.OpsPutAsync($"{PlansPath}/{other}", Edit(NewName(), 1, Row(unsold))).RefusalAsync());
    }

    [Fact]
    public async Task A_number_left_on_a_feature_that_became_a_switch_is_dropped_on_read_and_refused_when_sent()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var feature = await world.Factory.PutFeatureAsync(kind: "limit");
        var id = await world.Factory.PutPlanAsync(grid: (feature, 7));
        await TestDatabase.ExecuteAsync(
            sql.Main.Admin,
            $"UPDATE [plans].[Feature] SET [Kind] = 'switch', [UnitAr] = NULL, [UnitEn] = NULL, [Baseline] = NULL WHERE [Key] = '{feature}'");
        using var manager = await world.Factory.SignInWithAsync(Manager);

        var read = await manager.JsonAsync($"{PlansPath}/{id}");
        var row = read.GetProperty("features").EnumerateArray().Single(f => f.GetProperty("key").GetString() == feature);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("limit").ValueKind);

        Assert.Equal((400, "PLAN_SWITCH_HAS_LIMIT"), await manager.OpsPutAsync($"{PlansPath}/{id}", Edit(NewName(), 1, Row(feature, 7))).RefusalAsync());
        Assert.Equal(204, (await manager.OpsPutAsync($"{PlansPath}/{id}", Edit(NewName(), 1, Row(feature))).RefusalAsync()).Status);
        Assert.Null((await world.Factory.PlanAsync(id))!.Features[feature]);
    }

    [Fact]
    public async Task An_edit_based_on_an_older_plan_is_refused_with_nothing_written()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var id = await world.Factory.PutPlanAsync(grid: ("max_users", 3));
        using var manager = await world.Factory.SignInWithAsync(Manager);
        Assert.Equal(204, (await manager.OpsPutAsync($"{PlansPath}/{id}", Edit(NewName("Kept"), 1, Row("max_users", 4))).RefusalAsync()).Status);
        var kept = await world.Factory.PlanAsync(id);

        Assert.Equal((409, "PLAN_CHANGED_ELSEWHERE"), await manager.OpsPutAsync($"{PlansPath}/{id}", Edit(NewName("Lost"), 1, Row("max_users", 9))).RefusalAsync());

        var after = await world.Factory.PlanAsync(id);
        Assert.Equal(kept!.NameEn, after!.NameEn);
        Assert.Equal(4, after.Features["max_users"]);
        Assert.Equal(2, after.Version);
    }

    [Fact]
    public async Task A_badge_taken_by_another_plan_s_save_raises_this_plan_s_version()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        await sql.ClearBadgesAsync();
        var holder = await world.Factory.PutPlanAsync(badge: "best");
        var other = await world.Factory.PutPlanAsync();
        using var manager = await world.Factory.SignInWithAsync(Manager);

        Assert.Equal(204, (await manager.OpsPutAsync($"{PlansPath}/{other}", Edit(NewName(), 1).With("badge", "best")).RefusalAsync()).Status);

        // The holder's editor, still on version 1, must see that its badge went.
        Assert.Equal((409, "PLAN_CHANGED_ELSEWHERE"), await manager.OpsPutAsync($"{PlansPath}/{holder}", Edit(NewName(), 1).With("badge", "best")).RefusalAsync());
        Assert.Equal("best", (await world.Factory.PlanAsync(other))!.Badge);
    }

    [Fact]
    public async Task A_disabled_plan_takes_no_badge_and_a_code_in_the_body_is_never_read()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var id = await world.Factory.PutPlanAsync(disabled: true);
        var code = (await world.Factory.PlanAsync(id))!.Code;
        using var manager = await world.Factory.SignInWithAsync(Manager);

        Assert.Equal((409, "PLAN_BADGE_ON_DISABLED"), await manager.OpsPutAsync($"{PlansPath}/{id}", Edit(NewName(), 1).With("badge", "best")).RefusalAsync());
        Assert.Equal(204, (await manager.OpsPutAsync($"{PlansPath}/{id}", Edit(NewName(), 1).With("code", "stolen")).RefusalAsync()).Status);
        Assert.Equal(code, (await world.Factory.PlanAsync(id))!.Code);
    }

    [Fact]
    public async Task The_edit_records_only_what_changed_the_grid_per_feature_and_the_note()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var removed = await world.Factory.PutFeatureAsync();
        var limit = await world.Factory.PutFeatureAsync(kind: "limit");
        var added = await world.Factory.PutFeatureAsync();
        var name = NewName();
        var id = await world.Factory.PutPlanAsync(name, grid: [(removed, null), (limit, 5)]);
        var code = (await world.Factory.PlanAsync(id))!.Code;
        using var manager = await world.Factory.SignInWithAsync(Manager);

        var body = Edit(name, 1, Row(limit), Row(added)).With("monthlyEgp", 599m).With("note", "رفع السعر");
        Assert.Equal(204, (await manager.OpsPutAsync($"{PlansPath}/{id}", body).RefusalAsync()).Status);

        var entry = Assert.Single(await sql.PlanEntriesAsync(code, "plan.updated"));
        Assert.Contains("رفع السعر", entry.Facts, StringComparison.Ordinal);
        Assert.Contains("\"monthlyEgp\"", entry.Changes, StringComparison.Ordinal);
        Assert.Contains($"\"feature:{removed}\"", entry.Changes, StringComparison.Ordinal);
        Assert.Contains($"\"feature:{limit}\"", entry.Changes, StringComparison.Ordinal);
        Assert.Contains($"\"feature:{added}\"", entry.Changes, StringComparison.Ordinal);
        Assert.Contains("no limit", entry.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"nameEn\"", entry.Changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_plan_is_not_found()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var manager = await world.Factory.SignInWithAsync(Manager);
        Assert.Equal((404, "NOT_FOUND"), await manager.OpsPutAsync($"{PlansPath}/{Guid.NewGuid()}", Edit(NewName(), 1)).RefusalAsync());
        Assert.Equal((404, "NOT_FOUND"), await manager.OpsGetAsync($"{PlansPath}/{Guid.NewGuid()}/grid").RefusalAsync());
    }
}
