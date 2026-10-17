using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.SharedKernel.Access;
using Scs.Subscriptions.Contracts;
using static Scs.IntegrationTests.Fixtures.PlanFixtures;

namespace Scs.IntegrationTests.Subscriptions;

/// <summary>
/// Spec 014, User Story 1 (FR-002 to FR-007): what a clinic has, through the real plan and
/// catalogue — a disabled plan and a retired feature still applying, an operator's save applying
/// to the very next check, one reading per request. Test-first: plan entitlements.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClinicEntitlementsTests(SqlServerFixture sql)
{
    [Fact]
    public async Task The_answer_follows_the_plan_and_the_catalogue()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();

        var none = await factory.EntitlementsAsync(clinic);
        Assert.Null(none.PlanId);
        Assert.Null(none.Of("max_users").Limit);
        Assert.Equal(1, none.Of("max_doctors").Limit);
        Assert.True(none.Of("core_clinic").Included);
        Assert.False(none.Of("allow_analytics").Included);
        Assert.Equal(0, none.Of("no_such_feature").Limit);
        Assert.False(none.Of("no_such_feature").Included);

        await factory.GivePlanAsync(clinic, ("max_users", 10), ("allow_analytics", null));
        var some = await factory.EntitlementsAsync(clinic);

        Assert.NotNull(some.PlanId);
        Assert.Equal(10, some.Of("max_users").Limit);
        Assert.True(some.Of("allow_analytics").Included);
        Assert.True(some.Of("max_users").Enforced);
        Assert.False(some.Of("allow_analytics").Enforced);
    }

    [Fact]
    public async Task A_disabled_plan_and_a_retired_feature_keep_applying()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        var plan = await factory.PutPlanAsync(disabled: true, grid: [("max_doctors", 4), ("allow_insurance", null)]);
        await factory.SetPlanAsync(clinic, plan);

        await sql.ExecuteAsync(sql.AdminConnection, "UPDATE [plans].[Feature] SET IsRetired = 1 WHERE [Key] = 'allow_insurance'");
        try
        {
            var has = await factory.EntitlementsAsync(clinic);

            Assert.Equal(4, has.Of("max_doctors").Limit);
            Assert.True(has.Of("allow_insurance").Included);
        }
        finally
        {
            await sql.ExecuteAsync(sql.AdminConnection, "UPDATE [plans].[Feature] SET IsRetired = 0 WHERE [Key] = 'allow_insurance'");
        }
    }

    [Fact]
    public async Task A_plan_saved_by_an_operator_applies_to_the_very_next_check()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var clinic = await sql.Main.SeedClinicAsync();
        var name = NewName();
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView, OperatorPermissions.BillingManagePlans);
        var plan = await world.Factory.PutPlanAsync(nameEn: name, grid: [("max_users", 3)]);
        await world.Factory.SetPlanAsync(clinic, plan);
        Assert.Equal(3, (await world.Factory.EntitlementsAsync(clinic)).Of("max_users").Limit);

        var saved = await world.Factory.PlanAsync(plan);
        var body = NewPlan(nameEn: name, features: [Row("max_users", 8)]);
        body["version"] = saved!.Version;
        using var response = await manager.OpsPutAsync($"{PlansPath}/{plan}", body);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(8, (await world.Factory.EntitlementsAsync(clinic)).Of("max_users").Limit);
    }

    [Fact]
    public async Task One_request_reads_once_and_asks_for_an_established_clinic()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();

        await factory.ForClinicAsync(clinic, async services =>
        {
            var entitlements = services.GetRequiredService<IClinicEntitlements>();
            var first = await entitlements.CurrentAsync(CancellationToken.None);
            var second = await entitlements.CurrentAsync(CancellationToken.None);
            Assert.Same(first, second);
        });

        using var scope = factory.Services.CreateScope();
        var refusal = await Assert.ThrowsAsync<ScsProblemException>(
            () => scope.ServiceProvider.GetRequiredService<IClinicEntitlements>().CurrentAsync(CancellationToken.None));
        Assert.Equal("CLINIC_REQUIRED", refusal.Code);
        Assert.Null(scope.ServiceProvider.GetRequiredService<IClinicContext>().ClinicId);
    }

    [Fact]
    public async Task A_feature_that_follows_a_specialty_is_the_clinics_only_with_that_specialty()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var dental = await sql.Main.SeedClinicAsync();
        var skin = await sql.Main.SeedClinicAsync();
        var plan = await factory.PutPlanAsync(grid: [("dental_chart", null)]);
        await factory.SetPlanAsync(dental, plan);
        await factory.SetPlanAsync(skin, plan);
        await sql.SetSpecialtyAsync(dental, "dental");
        await sql.SetSpecialtyAsync(skin, "dermatology");

        Assert.True((await factory.EntitlementsAsync(dental)).Of("dental_chart").Included);
        Assert.False((await factory.EntitlementsAsync(skin)).Of("dental_chart").Included);
    }
}
