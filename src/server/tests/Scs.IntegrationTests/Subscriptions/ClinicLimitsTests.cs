using Microsoft.Extensions.DependencyInjection;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Plans;
using Scs.Subscriptions.Contracts;

namespace Scs.IntegrationTests.Subscriptions;

/// <summary>
/// Spec 014, User Story 6 (FR-024, SH-016; research R11): the eight counted limits in one read —
/// the catalogue's names, the clinic's numbers, what is used, and a limit nothing counts yet never
/// shown as 0.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClinicLimitsTests(SqlServerFixture sql)
{
    private static Task<IReadOnlyList<LimitReading>> ReadAsync(ApiFactory factory, Guid clinic) =>
        factory.ForClinicAsync(clinic, s => s.GetRequiredService<IClinicLimits>().ReadAsync(CancellationToken.None));

    [Fact]
    public async Task The_eight_come_in_order_with_their_names_numbers_and_use()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        var other = await sql.Main.SeedClinicAsync();
        await factory.GivePlanAsync(clinic, (FeatureKeys.MaxUsers, 10), (FeatureKeys.MessageCredits, 1000), (FeatureKeys.MaxDoctors, 5));
        for (var i = 0; i < 4; i++)
        {
            await factory.CreateAccountAsync(clinic);
        }

        await factory.CreateAccountAsync(other);
        await sql.ExecuteAsync(
            sql.Main.Crossing,
            $"""
             INSERT INTO [subscriptions].[MeterEntry] (Id, ClinicId, FeatureKey, Month, Amount, AtUtc, Purpose, [By], BeyondAllowance)
             SELECT NEWID(), '{clinic}', 'message_credits', FORMAT(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+02:00'), 'yyyy-MM'), 400, SYSDATETIMEOFFSET(), N'message.sent', N'job:test', 0
             """);

        var readings = await ReadAsync(factory, clinic);

        Assert.Equal(FeatureKeys.CountedLimits, readings.Select(r => r.Key));
        var users = readings[0];
        Assert.Equal(("المستخدمون", "Users", 10, 4, false), (users.NameAr, users.NameEn, users.Limit, users.Used, users.Monthly));
        var doctors = readings.Single(r => r.Key == FeatureKeys.MaxDoctors);
        Assert.Equal(5, doctors.Limit);
        Assert.Null(doctors.Used);
        var credits = readings.Single(r => r.Key == FeatureKeys.MessageCredits);
        Assert.Equal((1000, true), (credits.Limit, credits.Monthly));
        Assert.NotNull(credits.RenewsOn);
        Assert.Equal(1, credits.RenewsOn!.Value.Day);
        Assert.Null(readings.Single(r => r.Key == FeatureKeys.QueueScreen).Used);
    }

    [Fact]
    public async Task With_no_plan_the_eight_read_their_baselines()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();

        var readings = (await ReadAsync(factory, clinic)).ToDictionary(r => r.Key, r => r.Limit);

        Assert.Null(readings[FeatureKeys.MaxUsers]);
        Assert.Equal(1, readings[FeatureKeys.MaxDoctors]);
        Assert.Equal(1, readings[FeatureKeys.MaxBranches]);
        Assert.Equal(0, readings[FeatureKeys.QueueScreen]);
        Assert.Equal(0, readings[FeatureKeys.MessageCredits]);
    }
}
