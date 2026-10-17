using Microsoft.Extensions.DependencyInjection;
using Scs.Subscriptions.Contracts;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>
/// What the tests of spec 014 share: a clinic put on a plan made with 013's helpers, a feature of
/// the catalogue marked built for the length of a test (every database test runs in one
/// collection, one after another, so nobody else sees it), and a clinic's specialty.
/// </summary>
public static class SubscriptionFixtures
{
    /// <summary>Makes a plan with the grid given and puts the clinic on it through the module's operation.</summary>
    public static async Task<Guid> GivePlanAsync(this ApiFactory factory, Guid clinicId, params (string Key, int? Limit)[] grid)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var planId = await factory.PutPlanAsync(grid: grid);
        await factory.SetPlanAsync(clinicId, planId);
        return planId;
    }

    public static Task SetPlanAsync(this ApiFactory factory, Guid clinicId, Guid? planId) =>
        factory.ForClinicAsync(clinicId, services => services.GetRequiredService<IClinicPlans>().SetAsync(planId, CancellationToken.None));

    public static Task<ClinicEntitlements> EntitlementsAsync(this ApiFactory factory, Guid clinicId) =>
        factory.ForClinicAsync(clinicId, services => services.GetRequiredService<IClinicEntitlements>().CurrentAsync(CancellationToken.None));

    /// <summary>Marks catalogue features built (or upcoming) until the returned handle is disposed.</summary>
    public static async Task<IAsyncDisposable> MarkAsync(this SqlServerFixture sql, string status, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var list = string.Join(", ", keys.Select(k => $"'{k}'"));
        var before = new List<(string Key, string Status)>();
        foreach (var key in keys)
        {
            before.Add((key, (await TestDatabase.ScalarAsync<string>(sql.AdminConnection, $"SELECT [Status] FROM [plans].[Feature] WHERE [Key] = '{key}'"))!));
        }

        await sql.ExecuteAsync(sql.AdminConnection, $"UPDATE [plans].[Feature] SET [Status] = '{status}' WHERE [Key] IN ({list})");
        return new Restore(async () =>
        {
            foreach (var (key, was) in before)
            {
                await sql.ExecuteAsync(sql.AdminConnection, $"UPDATE [plans].[Feature] SET [Status] = '{was}' WHERE [Key] = '{key}'");
            }
        });
    }

    public static Task<IAsyncDisposable> BuiltAsync(this SqlServerFixture sql, params string[] keys) => sql.MarkAsync("built", keys);

    public static Task SetSpecialtyAsync(this SqlServerFixture sql, Guid clinicId, string? specialty)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var value = specialty is null ? "NULL" : $"'{specialty}'";
        return sql.ExecuteAsync(sql.Main.Crossing, $"UPDATE [tenancy].[Clinic] SET [Specialty] = {value} WHERE [Id] = '{clinicId}'");
    }

    private sealed class Restore(Func<Task> undo) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await undo();
    }
}
