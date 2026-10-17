using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.IntegrationTests.Fixtures;
using Scs.Subscriptions.Contracts;

namespace Scs.IntegrationTests.Subscriptions;

/// <summary>
/// Spec 014, FR-001 and FR-030 (contracts/enforcement-api.md §2): a clinic's link to its plan —
/// set, read, left empty — and every change of it in the clinic's trail and the platform's, in
/// one transaction with it. Another clinic's link is beyond reach.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClinicPlansTests(SqlServerFixture sql)
{
    private const string PlanSet = "subscription.plan-set";

    private static Task<Guid?> CurrentAsync(ApiFactory factory, Guid clinic) =>
        factory.ForClinicAsync(clinic, s => s.GetRequiredService<IClinicPlans>().CurrentAsync(CancellationToken.None));

    private Task<int> PlatformEntriesAsync(Guid clinic) =>
        TestDatabase.ScalarAsync<int>(
            sql.AdminConnection, $"SELECT COUNT(*) FROM [audit].[PlatformEntry] WHERE ActionCode = '{PlanSet}' AND TargetId = '{clinic}'");

    [Fact]
    public async Task A_clinic_is_put_on_a_plan_moved_and_left_with_none_each_change_recorded_twice()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        var first = await factory.PutPlanAsync();
        var second = await factory.PutPlanAsync();

        Assert.Null(await CurrentAsync(factory, clinic));

        await factory.SetPlanAsync(clinic, first);
        Assert.Equal(first, await CurrentAsync(factory, clinic));

        await factory.SetPlanAsync(clinic, first);
        await factory.SetPlanAsync(clinic, second);
        await factory.SetPlanAsync(clinic, null);
        Assert.Null(await CurrentAsync(factory, clinic));

        // Three changes; setting the same plan again recorded nothing.
        var entries = await sql.Main.EntriesAsync(clinic, PlanSet);
        Assert.Equal(3, entries.Count);
        Assert.Equal(3, await PlatformEntriesAsync(clinic));
        Assert.All(entries, e => Assert.Equal("clinic", e.TargetKind));
        Assert.Equal(JsonValue(entries[1].Change("plan"), "before"), JsonValue(entries[0].Change("plan"), "after"));
        Assert.Equal("", entries[2].FactsJson.GetProperty("plan").GetString());
    }

    [Fact]
    public async Task No_plan_set_on_a_clinic_with_none_records_nothing()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();

        await factory.SetPlanAsync(clinic, null);

        Assert.Empty(await sql.Main.EntriesAsync(clinic, PlanSet));
    }

    [Fact]
    public async Task A_plan_that_does_not_exist_is_refused_and_nothing_changes()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();

        var refusal = await Assert.ThrowsAsync<ScsProblemException>(() => factory.SetPlanAsync(clinic, Guid.NewGuid()));

        Assert.Equal("NOT_FOUND", refusal.Code);
        Assert.Null(await CurrentAsync(factory, clinic));
        Assert.Empty(await sql.Main.EntriesAsync(clinic, PlanSet));
    }

    [Fact]
    public async Task One_clinic_never_reads_or_changes_another_clinics_link()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var a = await sql.Main.SeedClinicAsync();
        var b = await sql.Main.SeedClinicAsync();
        var plan = await factory.PutPlanAsync();

        await factory.SetPlanAsync(a, plan);

        Assert.Null(await CurrentAsync(factory, b));
        await factory.SetPlanAsync(b, null);
        Assert.Equal(plan, await CurrentAsync(factory, a));
    }

    [Fact]
    public async Task A_plan_that_no_longer_resolves_reads_as_no_plan()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        var plan = await factory.PutPlanAsync(grid: [("max_doctors", 7)]);
        await factory.SetPlanAsync(clinic, plan);

        await sql.ExecuteAsync(sql.AdminConnection, $"DELETE FROM [plans].[PlanFeature] WHERE PlanId = '{plan}'; DELETE FROM [plans].[Plan] WHERE Id = '{plan}'");
        var has = await factory.EntitlementsAsync(clinic);

        Assert.Null(has.PlanId);
        Assert.Equal(1, has.Of("max_doctors").Limit);
    }

    [Fact]
    public async Task Sets_of_one_clinics_plan_at_once_take_their_turns_and_none_fails()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        var plans = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            plans.Add(await factory.PutPlanAsync());
        }

        // Ten at once on a clinic with no row (the key would clash), then ten more on its row (the version would).
        await Task.WhenAll(plans.Select(plan => factory.SetPlanAsync(clinic, plan)));
        await Task.WhenAll(plans.Select(plan => factory.SetPlanAsync(clinic, plan)));

        Assert.Contains(await CurrentAsync(factory, clinic) ?? Guid.Empty, plans);
        var entries = await sql.Main.EntriesAsync(clinic, PlanSet);
        Assert.InRange(entries.Count, 2, 20);
        Assert.Equal(entries.Count, await PlatformEntriesAsync(clinic));
    }

    private static string? JsonValue(System.Text.Json.JsonElement change, string name) =>
        change.TryGetProperty(name, out var value) ? value.GetString() : null;
}
