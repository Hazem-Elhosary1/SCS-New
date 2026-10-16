using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.Plans.Contracts;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.FeatureFixtures;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 012, User Story 4 (FR-019 to FR-021a; contracts/features-api.md §2): a sold feature is
/// retired, a retired one sold again, each recorded with before and after; nothing else of the
/// feature changes, and the catalogue's read for plans follows.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FeaturesRetireTests(SqlServerFixture sql)
{
    private static async Task<IReadOnlyList<CatalogueFeature>> SellableAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IFeatureCatalogue>().SellableAsync(default);
    }

    [Fact]
    public async Task Only_who_manages_plans_retires_or_sells_again()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var sold = await world.Factory.PutFeatureAsync();
        var retired = await world.Factory.PutFeatureAsync(retired: true);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);
        using var nobody = world.Factory.Browser();

        Assert.Equal(401, (await nobody.OpsPostAsync($"{Features}/{sold}/retire").RefusalAsync()).Status);
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPostAsync($"{Features}/{sold}/retire").RefusalAsync());
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPostAsync($"{Features}/{retired}/resume").RefusalAsync());

        Assert.False((await world.Factory.FeatureAsync(sold))!.IsRetired);
        Assert.True((await world.Factory.FeatureAsync(retired))!.IsRetired);
    }

    [Fact]
    public async Task Retiring_changes_the_retired_mark_alone_and_is_recorded_with_before_and_after()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var key = await world.Factory.PutFeatureAsync(kind: "limit");
        var (managerId, email, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.BillingManagePlans);
        using var manager = await world.Factory.SignInOperatorAsync(email);
        var before = await world.Factory.FeatureAsync(key);

        Assert.Equal(204, (await manager.OpsPostAsync($"{Features}/{key}/retire").RefusalAsync()).Status);

        var after = await world.Factory.FeatureAsync(key);
        Assert.Equal(before! with { IsRetired = true }, after);

        var entry = Assert.Single(await sql.FeatureEntriesAsync(key, "feature.retired"));
        Assert.Equal(("operator", managerId), (entry.ActorKind, entry.Actor));
        using var changes = JsonDocument.Parse(entry.Changes);
        var change = Assert.Single(changes.RootElement.EnumerateArray());
        Assert.Equal(("isRetired", "false", "true"), (
            change.GetProperty("field").GetString(), change.GetProperty("before").GetString(), change.GetProperty("after").GetString()));
    }

    [Fact]
    public async Task Only_a_sold_feature_is_retired_and_only_a_retired_one_sold_again()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var notSold = await world.Factory.PutFeatureAsync(sold: false);
        var retired = await world.Factory.PutFeatureAsync(retired: true);
        var sold = await world.Factory.PutFeatureAsync();
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        Assert.Equal((409, "FEATURE_STATE_CHANGED"), await manager.OpsPostAsync($"{Features}/{notSold}/retire").RefusalAsync());
        Assert.Equal((409, "FEATURE_STATE_CHANGED"), await manager.OpsPostAsync($"{Features}/{retired}/retire").RefusalAsync());
        Assert.Equal((409, "FEATURE_STATE_CHANGED"), await manager.OpsPostAsync($"{Features}/{sold}/resume").RefusalAsync());
        Assert.Equal((404, "NOT_FOUND"), await manager.OpsPostAsync($"{Features}/{NewKey()}/retire").RefusalAsync());

        Assert.Empty(await sql.FeatureEntriesAsync(notSold));
        Assert.Empty(await sql.FeatureEntriesAsync(retired));
    }

    [Fact]
    public async Task Retire_sell_again_and_retire_again_are_each_allowed_and_recorded_and_plans_read_them()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var key = await world.Factory.PutFeatureAsync();
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        Assert.Contains(await SellableAsync(world.Factory), f => f.Key == key);

        Assert.Equal(204, (await manager.OpsPostAsync($"{Features}/{key}/retire").RefusalAsync()).Status);
        Assert.DoesNotContain(await SellableAsync(world.Factory), f => f.Key == key);

        Assert.Equal(204, (await manager.OpsPostAsync($"{Features}/{key}/resume").RefusalAsync()).Status);
        var resumed = await world.Factory.FeatureAsync(key);
        Assert.Equal((false, true, 1), (resumed!.IsRetired, resumed.IsSold, resumed.Version));
        Assert.Contains(await SellableAsync(world.Factory), f => f.Key == key);

        Assert.Equal(204, (await manager.OpsPostAsync($"{Features}/{key}/retire").RefusalAsync()).Status);

        Assert.Equal(
            ["feature.retired", "feature.resumed", "feature.retired"],
            (await sql.FeatureEntriesAsync(key)).Select(e => e.Action));

        // Selling again is recorded as retiring is: who, what, and the mark before and after (FR-017).
        var resumedEntry = Assert.Single(await sql.FeatureEntriesAsync(key, "feature.resumed"));
        Assert.Equal(("operator", "ميزة تجريبية"), (resumedEntry.ActorKind, resumedEntry.TargetName));
        Assert.NotNull(resumedEntry.Actor);
        using var resumedChanges = JsonDocument.Parse(resumedEntry.Changes);
        var resumedChange = Assert.Single(resumedChanges.RootElement.EnumerateArray());
        Assert.Equal(("isRetired", "true", "false"), (
            resumedChange.GetProperty("field").GetString(), resumedChange.GetProperty("before").GetString(),
            resumedChange.GetProperty("after").GetString()));
    }
}
