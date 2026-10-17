using Microsoft.Extensions.DependencyInjection;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.Plans.Contracts;
using static Scs.IntegrationTests.Fixtures.FeatureFixtures;
using static Scs.IntegrationTests.Fixtures.PlanFixtures;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 013, FR-025 (data-model.md "Published"): other modules read a plan and what it includes
/// through <see cref="IPlanBook"/>, never a copy of their own; a disabled plan is never offered to
/// a clinic (ق-٤٧); and until clinics have plans, <see cref="IPlanSubscribers"/> answers none.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlanBookContractTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_plan_reads_with_what_it_includes_and_each_limit()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var channel = await world.Factory.PutFeatureAsync();
        var quota = await world.Factory.PutFeatureAsync(kind: "limit");
        var id = await world.Factory.PutPlanAsync(grid: [(channel, null), (quota, 25), ("max_users", null)]);

        using var scope = world.Factory.Services.CreateScope();
        var book = scope.ServiceProvider.GetRequiredService<IPlanBook>();
        var plan = await book.FindAsync(id, CancellationToken.None);

        Assert.NotNull(plan);
        Assert.Equal("active", plan.State);
        var included = plan.Features.ToDictionary(f => f.Key, StringComparer.Ordinal);
        Assert.Equal(("switch", (int?)null), (included[channel].Kind, included[channel].Limit));
        Assert.Equal(("limit", (int?)25), (included[quota].Kind, included[quota].Limit));
        Assert.Equal(("limit", (int?)null), (included["max_users"].Kind, included["max_users"].Limit));

        Assert.Null(await book.FindAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Only_active_plans_are_offered_to_a_clinic_by_monthly_price()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var dear = await world.Factory.PutPlanAsync(monthly: 9_000m);
        var cheap = await world.Factory.PutPlanAsync(monthly: 1m);
        var off = await world.Factory.PutPlanAsync(disabled: true);

        using var scope = world.Factory.Services.CreateScope();
        var offered = await scope.ServiceProvider.GetRequiredService<IPlanBook>().AssignableAsync(CancellationToken.None);
        var ids = offered.Select(p => p.Id).ToList();

        Assert.DoesNotContain(off, ids);
        Assert.True(ids.IndexOf(cheap) < ids.IndexOf(dear));
        Assert.Equal(offered.OrderBy(p => p.MonthlyEgp).ThenBy(p => p.NameEn, StringComparer.Ordinal).Select(p => p.Id), ids);

        // A disabled plan is still read: a clinic on it keeps it (ق-٤٧).
        Assert.Equal("disabled", (await scope.ServiceProvider.GetRequiredService<IPlanBook>().FindAsync(off, CancellationToken.None))!.State);
    }

    [Fact]
    public async Task Until_clinics_have_plans_no_clinic_is_on_any()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var id = await world.Factory.PutPlanAsync();

        using var scope = world.Factory.Services.CreateScope();
        var clinics = scope.ServiceProvider.GetRequiredService<IPlanSubscribers>();
        Assert.Empty(await clinics.CountsAsync([id], CancellationToken.None));
        Assert.Null(await clinics.RevenueAsync(CancellationToken.None));
        Assert.Empty(await clinics.ClinicsAsync(id, CancellationToken.None));
    }
}
