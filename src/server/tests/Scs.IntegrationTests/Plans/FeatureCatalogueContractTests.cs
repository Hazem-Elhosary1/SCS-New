using Microsoft.Extensions.DependencyInjection;
using Scs.IntegrationTests.Fixtures;
using Scs.Plans.Contracts;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 012, FR-025 and FR-026 (contracts/features-api.md §4): the one read other modules make of
/// the catalogue — a key is a feature or unknown, and what a plan may take is the sold features
/// that are not retired, in the catalogue's order.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FeatureCatalogueContractTests(SqlServerFixture sql)
{
    private static async Task<T> ReadAsync<T>(ApiFactory factory, Func<IFeatureCatalogue, Task<T>> read)
    {
        using var scope = factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<IFeatureCatalogue>());
    }

    [Fact]
    public async Task A_key_is_a_feature_with_what_it_is_or_unknown()
    {
        using var world = await OpsWorld.CreateAsync(sql);

        var doctors = await ReadAsync(world.Factory, c => c.FindAsync("max_doctors", default));
        Assert.NotNull(doctors);
        Assert.Equal(("limit", "طبيب", "doctors", 1, (string?)null, "upcoming", false), (
            doctors.Kind, doctors.UnitAr, doctors.UnitEn, doctors.Baseline, doctors.Specialty, doctors.Status, doctors.Monthly));

        var chart = await ReadAsync(world.Factory, c => c.FindAsync("dental_chart", default));
        Assert.Equal("dental", chart!.Specialty);
        Assert.True((await ReadAsync(world.Factory, c => c.FindAsync("ai_voice_notes", default)))!.Monthly);

        Assert.Null(await ReadAsync(world.Factory, c => c.FindAsync("no_such_feature", default)));
        Assert.Null(await ReadAsync(world.Factory, c => c.FindAsync("", default)));
    }

    [Fact]
    public async Task A_retired_feature_is_still_found_but_not_offered_and_a_not_sold_one_is_never_offered()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var retired = await world.Factory.PutFeatureAsync(retired: true);

        Assert.True((await ReadAsync(world.Factory, c => c.FindAsync(retired, default)))!.IsRetired);

        var sellable = await ReadAsync(world.Factory, c => c.SellableAsync(default));
        Assert.DoesNotContain(sellable, f => f.Key == retired);
        Assert.DoesNotContain(sellable, f => f.Key is "core_clinic" or "specialty_exam_modules");
        Assert.Contains(sellable, f => f.Key == "max_users");
        Assert.All(sellable, f => Assert.True(f.IsSold && !f.IsRetired));
    }
}
