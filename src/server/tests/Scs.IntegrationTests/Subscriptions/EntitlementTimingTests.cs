using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Plans;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Plans;
using Scs.Subscriptions.Contracts;
using Xunit.Abstractions;

namespace Scs.IntegrationTests.Subscriptions;

/// <summary>
/// Spec 014, SC-008: checking a clinic's plan adds no wait a person can notice — the check (the
/// plan, the catalogue, the specialty, and a count where one applies) answers within 50 ms for 95%
/// of requests on the local stack. Each check is a request's first: a new scope, nothing kept.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EntitlementTimingTests(SqlServerFixture sql, ITestOutputHelper output)
{
    private const int Checks = 200;

    [Fact]
    public async Task Ninety_five_checks_in_a_hundred_answer_within_fifty_milliseconds()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        await factory.GivePlanAsync(clinic, (FeatureKeys.MaxUsers, 50), (FeatureKeys.AllowAnalytics, null));
        await factory.CreateAccountAsync(clinic);

        // Warm the connection pool and the model once, as a running platform would be.
        await factory.EntitlementsAsync(clinic);

        var times = new List<double>(Checks);
        for (var i = 0; i < Checks; i++)
        {
            var started = Stopwatch.GetTimestamp();
            await factory.ForClinicAsync(clinic, async services =>
            {
                await services.GetRequiredService<IClinicEntitlements>().EnsureIncludedAsync(FeatureKeys.AllowAnalytics, CancellationToken.None);
                await services.GetRequiredService<IPlanAnswers>().LimitOfAsync(FeatureKeys.MaxUsers, CancellationToken.None);
                await services.GetServices<ILimitCounter>().Single(c => c.FeatureKey == FeatureKeys.MaxUsers).CountAsync(CancellationToken.None);
            });
            times.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        times.Sort();
        var p95 = times[(int)Math.Ceiling(Checks * 0.95) - 1];
        output.WriteLine($"Plan check over {Checks} requests: median {times[Checks / 2]:F1} ms, 95th percentile {p95:F1} ms.");

        Assert.True(p95 <= 50, $"The 95th percentile is {p95:F1} ms.");
    }
}
