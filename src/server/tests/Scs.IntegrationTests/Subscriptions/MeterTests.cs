using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Plans;
using Scs.Subscriptions.Contracts;

namespace Scs.IntegrationTests.Subscriptions;

/// <summary>
/// Spec 014, User Story 4 (FR-019 to FR-023; research R10): the meter of the four monthly limits
/// — a use taken, a use refused, a promise completed past the allowance, a give-back, a plan
/// changed mid-month, the month restarting at midnight in Cairo — and many uses at once never
/// taking more than is left. Test-first: plan limits.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MeterTests(SqlServerFixture sql)
{
    private const string Credits = FeatureKeys.MessageCredits;

    private static Task<T> MeterAsync<T>(ApiFactory factory, Guid clinic, Func<IMonthlyMeter, Task<T>> work) =>
        factory.ForClinicAsync(clinic, s => work(s.GetRequiredService<IMonthlyMeter>()));

    private static Task<Guid> UseAsync(ApiFactory factory, Guid clinic, int amount, bool promise = false) =>
        MeterAsync(factory, clinic, m => m.UseAsync(new MeterUse(Credits, amount, "message.sent", "msg-1", promise), CancellationToken.None));

    private static Task<MeterReading> ReadAsync(ApiFactory factory, Guid clinic, string? month = null) =>
        MeterAsync(factory, clinic, m => m.ReadAsync(Credits, month, CancellationToken.None));

    private async Task<(ApiFactory Factory, Guid Clinic, Guid Plan)> WorldAsync(int? credits)
    {
        await sql.EnsureMigratedAsync();
        var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();
        var plan = await factory.GivePlanAsync(clinic, (Credits, credits));
        return (factory, clinic, plan);
    }

    [Fact]
    public async Task A_use_is_taken_and_one_the_allowance_cannot_cover_is_refused_whole()
    {
        await using var built = await sql.BuiltAsync(Credits);
        var (factory, clinic, _) = await WorldAsync(1000);
        using var _f = factory;

        var use = await UseAsync(factory, clinic, 400);
        var refusal = await Assert.ThrowsAsync<ScsProblemException>(() => UseAsync(factory, clinic, 700));
        var reading = await ReadAsync(factory, clinic);

        Assert.Equal(("PLAN_LIMIT_REACHED", 409), (refusal.Code, refusal.Status));
        Assert.Equal(Credits, refusal.Extensions!["feature"]);
        Assert.Equal(1000, refusal.Extensions!["limit"]);
        Assert.Equal((1000, 400, 600, 0), (reading.Allowance, reading.Used, reading.Left, reading.Overrun));

        var stored = await TestDatabase.ScalarAsync<string>(
            sql.Main.Crossing, $"SELECT CONCAT(Purpose, '|', Reference, '|', LEFT([By], 4)) FROM [subscriptions].[MeterEntry] WHERE Id = '{use}'");
        Assert.Equal("message.sent|msg-1|job:", stored);
    }

    [Fact]
    public async Task A_promise_is_completed_past_the_allowance_and_marked_so()
    {
        await using var built = await sql.BuiltAsync(Credits);
        var (factory, clinic, _) = await WorldAsync(100);
        using var _f = factory;
        await UseAsync(factory, clinic, 90);

        var promised = await UseAsync(factory, clinic, 30, promise: true);
        var reading = await ReadAsync(factory, clinic);

        Assert.Equal((120, 0, 20), (reading.Used, reading.Left, reading.Overrun));
        Assert.True(await TestDatabase.ScalarAsync<bool>(sql.Main.Crossing, $"SELECT BeyondAllowance FROM [subscriptions].[MeterEntry] WHERE Id = '{promised}'"));
    }

    [Fact]
    public async Task A_use_given_back_returns_to_its_month_and_never_more_than_it_took()
    {
        await using var built = await sql.BuiltAsync(Credits);
        var (factory, clinic, _) = await WorldAsync(1000);
        using var _f = factory;
        var use = await UseAsync(factory, clinic, 50);

        await MeterAsync<object?>(factory, clinic, async m => { await m.GiveBackAsync(use, 20, CancellationToken.None); return null; });
        var refusal = await Assert.ThrowsAsync<ScsProblemException>(
            () => MeterAsync<object?>(factory, clinic, async m => { await m.GiveBackAsync(use, 31, CancellationToken.None); return null; }));

        Assert.Equal("METER_GIVE_BACK_TOO_MUCH", refusal.Code);
        Assert.Equal(30, (await ReadAsync(factory, clinic)).Used);
    }

    [Fact]
    public async Task A_plan_changed_mid_month_gives_its_allowance_and_keeps_what_was_used()
    {
        await using var built = await sql.BuiltAsync(Credits);
        var (factory, clinic, _) = await WorldAsync(100);
        using var _f = factory;
        await UseAsync(factory, clinic, 80);

        await factory.GivePlanAsync(clinic, (Credits, 500));
        var reading = await ReadAsync(factory, clinic);

        Assert.Equal((500, 80, 420), (reading.Allowance, reading.Used, reading.Left));
    }

    [Fact]
    public async Task With_no_plan_every_use_is_refused_and_with_no_number_none_is()
    {
        await using var built = await sql.BuiltAsync(Credits);
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var bare = await sql.Main.SeedClinicAsync();
        var unlimited = await sql.Main.SeedClinicAsync();
        await factory.GivePlanAsync(unlimited, (Credits, null));

        Assert.Equal("PLAN_LIMIT_REACHED", (await Assert.ThrowsAsync<ScsProblemException>(() => UseAsync(factory, bare, 1))).Code);
        await UseAsync(factory, unlimited, 100_000);
    }

    [Fact]
    public async Task Only_the_four_monthly_limits_and_whole_units_are_metered()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => MeterAsync(factory, clinic, m => m.UseAsync(new MeterUse(FeatureKeys.MaxUsers, 1, "x"), CancellationToken.None)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => MeterAsync(factory, clinic, m => m.UseAsync(new MeterUse(Credits, 0, "x"), CancellationToken.None)));
    }

    [Fact]
    public async Task The_month_restarts_at_midnight_in_Cairo_and_last_month_reads_as_it_was()
    {
        await using var built = await sql.BuiltAsync(Credits);
        var (factory, clinic, _) = await WorldAsync(100);
        using var _f = factory;
        await UseAsync(factory, clinic, 70);
        var before = await ReadAsync(factory, clinic);

        // Move the clock to the first moment of next month in Cairo.
        var now = factory.Clock.GetUtcNow();
        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var local = TimeZoneInfo.ConvertTime(now, cairo);
        var firstNext = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1);
        var target = new DateTimeOffset(firstNext, cairo.GetUtcOffset(firstNext));
        factory.Clock.Advance(target - now + TimeSpan.FromSeconds(1));

        var after = await ReadAsync(factory, clinic);
        var last = await ReadAsync(factory, clinic, before.Month);

        Assert.NotEqual(before.Month, after.Month);
        Assert.Equal((100, 0, 100), (after.Allowance, after.Used, after.Left));
        Assert.Equal(70, last.Used);
        await UseAsync(factory, clinic, 100);
    }

    [Fact]
    public async Task Twenty_uses_at_once_that_only_one_fits_take_one()
    {
        await using var built = await sql.BuiltAsync(Credits);
        var (factory, clinic, _) = await WorldAsync(10);
        using var _f = factory;

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            try
            {
                await UseAsync(factory, clinic, 10);
                return true;
            }
            catch (ScsProblemException refusal) when (refusal.Code == "PLAN_LIMIT_REACHED")
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(10, (await ReadAsync(factory, clinic)).Used);
    }

    [Fact]
    public async Task One_clinics_meter_is_beyond_anothers_reach()
    {
        await using var built = await sql.BuiltAsync(Credits);
        var (factory, a, plan) = await WorldAsync(100);
        using var _f = factory;
        var b = await sql.Main.SeedClinicAsync();
        await factory.SetPlanAsync(b, plan);
        var use = await UseAsync(factory, a, 40);

        Assert.Equal(0, (await ReadAsync(factory, b)).Used);
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<ScsProblemException>(
            () => MeterAsync<object?>(factory, b, async m => { await m.GiveBackAsync(use, 1, CancellationToken.None); return null; }))).Code);
    }
}
