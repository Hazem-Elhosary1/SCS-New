using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.FeatureFixtures;
using static Scs.IntegrationTests.Fixtures.PlanFixtures;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 013, User Story 5 (FR-021, FR-022, FR-024; contracts/plans-api.md §2): a plan is disabled
/// with an optional reason and enabled again — its clinics untouched, its badge gone for good —
/// with «إدارة الباقات والاشتراكات» and by nobody else.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlansDisableTests(SqlServerFixture sql)
{
    private static string[] Manager { get; } = [OperatorPermissions.BillingView, OperatorPermissions.BillingManagePlans];

    [Fact]
    public async Task Only_who_manages_plans_disables_and_enables()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var id = await world.Factory.PutPlanAsync();
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPostAsync($"{PlansPath}/{id}/disable", new { reason = (string?)null }).RefusalAsync());
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPostAsync($"{PlansPath}/{id}/enable").RefusalAsync());
        Assert.Equal("active", (await world.Factory.PlanAsync(id))!.State);
    }

    [Fact]
    public async Task Disabling_keeps_the_reason_and_the_time_and_removes_the_badge_without_a_new_version()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        await sql.ClearBadgesAsync();
        var id = await world.Factory.PutPlanAsync(badge: "best");
        using var manager = await world.Factory.SignInWithAsync(Manager);

        Assert.Equal(204, (await manager.OpsPostAsync($"{PlansPath}/{id}/disable", new { reason = "  استُبدلت بالأساسية  " }).RefusalAsync()).Status);

        var plan = await world.Factory.PlanAsync(id);
        Assert.Equal("disabled", plan!.State);
        Assert.Equal("استُبدلت بالأساسية", plan.DisabledReason);
        Assert.NotNull(plan.DisabledAtUtc);
        Assert.Null(plan.Badge);
        Assert.Equal(1, plan.Version);

        var entry = Assert.Single(await sql.PlanEntriesAsync(plan.Code, "plan.disabled"));
        Assert.Contains("\"state\"", entry.Changes, StringComparison.Ordinal);
        Assert.Contains("\"badge\"", entry.Changes, StringComparison.Ordinal);
        Assert.Contains("استُبدلت بالأساسية", entry.Changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_reason_is_none_and_one_over_five_hundred_is_refused()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plain = await world.Factory.PutPlanAsync();
        var refused = await world.Factory.PutPlanAsync();
        using var manager = await world.Factory.SignInWithAsync(Manager);

        Assert.Equal(204, (await manager.OpsPostAsync($"{PlansPath}/{plain}/disable", new { reason = "   " }).RefusalAsync()).Status);
        Assert.Null((await world.Factory.PlanAsync(plain))!.DisabledReason);

        Assert.Equal((400, "VALIDATION_FAILED"), await manager.OpsPostAsync($"{PlansPath}/{refused}/disable", new { reason = new string('س', 501) }).RefusalAsync());
        Assert.Equal("active", (await world.Factory.PlanAsync(refused))!.State);
    }

    [Fact]
    public async Task Disabling_again_keeps_the_first_reason_and_time_and_records_nothing()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var id = await world.Factory.PutPlanAsync();
        using var manager = await world.Factory.SignInWithAsync(Manager);
        await manager.OpsPostAsync($"{PlansPath}/{id}/disable", new { reason = "الأول" });
        var first = await world.Factory.PlanAsync(id);

        Assert.Equal(204, (await manager.OpsPostAsync($"{PlansPath}/{id}/disable", new { reason = "الثاني" }).RefusalAsync()).Status);

        var again = await world.Factory.PlanAsync(id);
        Assert.Equal("الأول", again!.DisabledReason);
        Assert.Equal(first!.DisabledAtUtc, again.DisabledAtUtc);
        Assert.Single(await sql.PlanEntriesAsync(again.Code, "plan.disabled"));
    }

    [Fact]
    public async Task Enabling_clears_the_reason_and_time_and_does_not_bring_the_badge_back()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        await sql.ClearBadgesAsync();
        var id = await world.Factory.PutPlanAsync(badge: "most_popular");
        using var manager = await world.Factory.SignInWithAsync(Manager);
        await manager.OpsPostAsync($"{PlansPath}/{id}/disable", new { reason = "مؤقتًا" });

        Assert.Equal(204, (await manager.OpsPostAsync($"{PlansPath}/{id}/enable").RefusalAsync()).Status);

        var plan = await world.Factory.PlanAsync(id);
        Assert.Equal("active", plan!.State);
        Assert.Null(plan.DisabledReason);
        Assert.Null(plan.DisabledAtUtc);
        Assert.Null(plan.Badge);
        Assert.Equal(1, plan.Version);
        Assert.Single(await sql.PlanEntriesAsync(plan.Code, "plan.enabled"));

        // Enabling an active plan changes nothing and records nothing.
        Assert.Equal(204, (await manager.OpsPostAsync($"{PlansPath}/{id}/enable").RefusalAsync()).Status);
        Assert.Single(await sql.PlanEntriesAsync(plan.Code, "plan.enabled"));
    }

    [Fact]
    public async Task An_unknown_plan_is_not_found()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var manager = await world.Factory.SignInWithAsync(Manager);
        Assert.Equal((404, "NOT_FOUND"), await manager.OpsPostAsync($"{PlansPath}/{Guid.NewGuid()}/disable", new { reason = (string?)null }).RefusalAsync());
        Assert.Equal((404, "NOT_FOUND"), await manager.OpsPostAsync($"{PlansPath}/{Guid.NewGuid()}/enable").RefusalAsync());
    }
}
