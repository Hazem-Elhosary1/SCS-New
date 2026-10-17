using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.Identity.Contracts;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Plans;

namespace Scs.IntegrationTests.Subscriptions;

/// <summary>
/// Spec 014, FR-013 and SC-002 (ق-أ-١٩): of many requests for the last seat at one moment,
/// exactly one succeeds and every other is refused with the limit named.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SeatRaceTests(SqlServerFixture sql)
{
    private static async Task<(int Succeeded, int Refused)> RaceAsync(IEnumerable<Func<Task>> requests)
    {
        var results = await Task.WhenAll(requests.Select(async request =>
        {
            try
            {
                await request();
                return "ok";
            }
            catch (ScsProblemException refusal)
            {
                return refusal.Code;
            }
        }));

        Assert.All(results, r => Assert.True(r is "ok" or "PLAN_LIMIT_REACHED", r));
        return (results.Count(r => r == "ok"), results.Count(r => r == "PLAN_LIMIT_REACHED"));
    }

    [Fact]
    public async Task Twenty_creations_for_one_free_seat_give_one_account()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        await factory.GivePlanAsync(clinic, (FeatureKeys.MaxUsers, 3));
        await factory.CreateAccountAsync(clinic);
        await factory.CreateAccountAsync(clinic);

        var (succeeded, refused) = await RaceAsync(Enumerable.Range(0, 20).Select(_ => (Func<Task>)(() => factory.CreateAccountAsync(clinic))));

        Assert.Equal((1, 19), (succeeded, refused));
        Assert.Equal(3, await TestDatabase.ScalarAsync<int>(sql.Main.Crossing, $"SELECT COUNT(*) FROM [identity].[StaffAccount] WHERE ClinicId = '{clinic}'"));
    }

    [Fact]
    public async Task Twenty_re_enables_for_one_free_seat_give_one_active_account()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        await factory.GivePlanAsync(clinic, (FeatureKeys.MaxUsers, 1));
        var disabled = new List<Guid>();
        for (var i = 0; i < 20; i++)
        {
            disabled.Add((await factory.CreateAccountAsync(clinic, StaffAccountStates.Disabled)).Id);
        }

        var (succeeded, refused) = await RaceAsync(disabled.Select(id => (Func<Task>)(() => factory.ForClinicAsync(
            clinic, s => s.GetRequiredService<IStaffAccounts>().SetStateAsync(id, StaffAccountStates.Active, CancellationToken.None)))));

        Assert.Equal((1, 19), (succeeded, refused));
    }
}
