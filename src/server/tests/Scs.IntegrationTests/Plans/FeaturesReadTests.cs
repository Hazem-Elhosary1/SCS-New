using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.FeatureFixtures;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 012, User Story 1 (FR-002 to FR-008; contracts/features-api.md §1): the catalogue is read
/// with «عرض الباقات والفواتير» and by nobody else, by group then order, retired ones too, with the
/// cards counted from it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FeaturesReadTests(SqlServerFixture sql)
{
    private static readonly string[] Groups = ["core", "channels", "reports", "money", "clinical", "ai", "growth"];

    [Fact]
    public async Task Only_who_may_view_plans_reads_the_catalogue_a_feature_and_where_it_is_used()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var key = await world.Factory.PutFeatureAsync();
        string[] reads = [Features, $"{Features}/{key}", $"{Features}/{key}/plans"];

        using var nobody = world.Factory.Browser();
        using var without = await world.Factory.SignInWithAsync(OperatorPermissions.TicketsView);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        foreach (var read in reads)
        {
            Assert.Equal(401, (await nobody.OpsGetAsync(read).RefusalAsync()).Status);
            Assert.Equal((403, "PERMISSION_REQUIRED"), await without.OpsGetAsync(read).RefusalAsync());
            Assert.Equal(200, (await viewer.OpsGetAsync(read).RefusalAsync()).Status);
        }
    }

    [Fact]
    public async Task A_clinic_s_session_reads_nothing_of_the_catalogue()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var clinic = await sql.Main.SeedClinicAsync();
        var (_, staffEmail) = await world.Factory.CreateAccountAsync(clinic);
        using var staff = await world.Factory.SignInAsAsync(staffEmail, SignIn.Password);

        // A clinic's session is no session here (spec 011).
        Assert.Equal(401, (await staff.OpsGetAsync(Features).RefusalAsync()).Status);
        Assert.Equal(401, (await staff.OpsPostAsync(Features, NewSwitch()).RefusalAsync()).Status);
    }

    [Fact]
    public async Task The_catalogue_is_by_group_then_order_retired_ones_included_with_every_field()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var retired = await world.Factory.PutFeatureAsync(group: "money", retired: true);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var page = await viewer.JsonAsync(Features);
        var items = page.GetProperty("items").EnumerateArray().ToList();

        var order = items.Select(i => (Array.IndexOf(Groups, i.GetProperty("group").GetString()), i.GetProperty("sortOrder").GetInt32())).ToList();
        Assert.Equal(order.OrderBy(o => o.Item1).ThenBy(o => o.Item2), order);

        var row = items.Single(i => i.GetProperty("key").GetString() == retired);
        Assert.True(row.GetProperty("isRetired").GetBoolean());
        foreach (var field in new[]
                 {
                     "nameAr", "nameEn", "descriptionAr", "descriptionEn", "group", "kind", "unitAr", "unitEn", "baseline", "specialty",
                     "status", "isSold", "sortOrder", "monthly", "plans", "version",
                 })
        {
            Assert.True(row.TryGetProperty(field, out _), field);
        }

        Assert.Equal(0, row.GetProperty("plans").GetInt32());

        var credits = items.Single(i => i.GetProperty("key").GetString() == "message_credits");
        Assert.True(credits.GetProperty("monthly").GetBoolean());
        Assert.False(items.Single(i => i.GetProperty("key").GetString() == "max_users").GetProperty("monthly").GetBoolean());
    }

    [Fact]
    public async Task The_cards_are_counted_from_the_catalogue_and_say_whether_the_caller_manages()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var page = await viewer.JsonAsync(Features);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        var cards = page.GetProperty("cards");

        Assert.Equal(items.Count, cards.GetProperty("total").GetInt32());
        Assert.Equal(items.Select(i => i.GetProperty("group").GetString()).Distinct().Count(), cards.GetProperty("groups").GetInt32());
        Assert.Equal(items.Count(i => i.GetProperty("status").GetString() == "built"), cards.GetProperty("built").GetInt32());
        Assert.Equal(items.Count(i => i.GetProperty("status").GetString() == "upcoming"), cards.GetProperty("upcoming").GetInt32());
        // Counted from the plans since spec 013 (which made them): the features in a plan, and those plans.
        Assert.Equal(items.Count(i => i.GetProperty("plans").GetInt32() > 0), cards.GetProperty("soldInPlans").GetInt32());
        var plans = await TestDatabase.ReadColumnAsync(sql.Main.Admin, "SELECT CAST(COUNT(DISTINCT [PlanId]) AS nvarchar(10)) FROM [plans].[PlanFeature]");
        Assert.Equal(int.Parse(plans.Single(), System.Globalization.CultureInfo.InvariantCulture), cards.GetProperty("plans").GetInt32());

        Assert.False(page.GetProperty("can").GetProperty("manage").GetBoolean());
        Assert.True((await manager.JsonAsync(Features)).GetProperty("can").GetProperty("manage").GetBoolean());
    }

    [Fact]
    public async Task One_feature_reads_as_its_row_an_unknown_key_is_not_found_and_no_plan_uses_it_yet()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var one = await viewer.JsonAsync($"{Features}/max_doctors");
        Assert.Equal(("max_doctors", "limit", 1), (one.GetProperty("key").GetString(), one.GetProperty("kind").GetString(), one.GetProperty("baseline").GetInt32()));

        Assert.Equal((404, "NOT_FOUND"), await viewer.OpsGetAsync($"{Features}/no_such_feature").RefusalAsync());
        Assert.Equal((404, "NOT_FOUND"), await viewer.OpsGetAsync($"{Features}/no_such_feature/plans").RefusalAsync());

        var used = await viewer.JsonAsync($"{Features}/max_doctors/plans");
        Assert.Empty(used.GetProperty("items").EnumerateArray());
        Assert.Equal(0, used.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Three_hundred_features_answer_within_a_second()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);
        var have = (await viewer.JsonAsync(Features)).GetProperty("items").GetArrayLength();
        for (var i = have; i < 300; i++)
        {
            await world.Factory.PutFeatureAsync(group: "growth");
        }

        // A warm-up, then the best of three: the goal is checked, and a busy machine does not fail it.
        await viewer.JsonAsync(Features);
        var best = long.MaxValue;
        var items = 0;
        for (var i = 0; i < 3; i++)
        {
            var clock = Stopwatch.StartNew();
            using var answer = await viewer.OpsGetAsync(Features);
            clock.Stop();
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            best = Math.Min(best, clock.ElapsedMilliseconds);
            items = JsonDocument.Parse(await answer.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength();
        }

        Assert.True(best < 1000, $"{best} ms");
        Assert.True(items >= 300);
    }
}
