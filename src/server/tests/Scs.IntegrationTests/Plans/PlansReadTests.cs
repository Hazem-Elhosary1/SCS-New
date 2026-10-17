using System.Diagnostics;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.Plans.Contracts;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.FeatureFixtures;
using static Scs.IntegrationTests.Fixtures.PlanFixtures;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 013, User Story 1 (FR-002 to FR-008; contracts/plans-api.md §1): the plans are read with
/// «عرض الباقات والفواتير» and by nobody else — active ones by price, then disabled ones — with the
/// users each allows, the cards, and the clinics as the clinics' module counts them.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlansReadTests(SqlServerFixture sql)
{
    private static List<JsonElement> Items(JsonElement page) => [.. page.GetProperty("items").EnumerateArray()];

    private static JsonElement Of(JsonElement page, Guid id) =>
        Items(page).Single(i => i.GetProperty("id").GetGuid() == id);

    [Fact]
    public async Task Only_who_may_view_plans_reads_the_list_and_a_plan()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var id = await world.Factory.PutPlanAsync();
        string[] reads = [PlansPath, $"{PlansPath}/{id}"];

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
    public async Task A_clinic_s_session_reads_no_plan()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var clinic = await sql.Main.SeedClinicAsync();
        var (_, staffEmail) = await world.Factory.CreateAccountAsync(clinic);
        using var staff = await world.Factory.SignInAsAsync(staffEmail, SignIn.Password);

        // A clinic's session is no session here (spec 011).
        Assert.Equal(401, (await staff.OpsGetAsync(PlansPath).RefusalAsync()).Status);
        Assert.Equal(401, (await staff.OpsPostAsync(PlansPath, NewPlan()).RefusalAsync()).Status);
    }

    [Fact]
    public async Task Active_plans_come_first_each_by_monthly_price_with_every_field()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var dear = await world.Factory.PutPlanAsync(monthly: 999m);
        var cheap = await world.Factory.PutPlanAsync(monthly: 99m);
        var off = await world.Factory.PutPlanAsync(monthly: 9m, disabled: true);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var page = await viewer.JsonAsync(PlansPath);
        var order = Items(page)
            .Select(i => (Off: i.GetProperty("state").GetString() == "active" ? 0 : 1, Price: i.GetProperty("monthlyEgp").GetDecimal()))
            .ToList();
        Assert.Equal(order.OrderBy(o => o.Off).ThenBy(o => o.Price), order);

        var ids = Items(page).Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.True(ids.IndexOf(cheap) < ids.IndexOf(dear));
        Assert.True(ids.IndexOf(dear) < ids.IndexOf(off));

        var row = Of(page, cheap);
        foreach (var field in new[]
                 {
                     "code", "nameAr", "nameEn", "monthlyEgp", "annualEgp", "monthlyUsd", "annualUsd", "badge", "state", "users", "clinics",
                     "createdAt", "updatedAt", "version",
                 })
        {
            Assert.True(row.TryGetProperty(field, out _), field);
        }

        Assert.Equal(99m, row.GetProperty("monthlyEgp").GetDecimal());
        Assert.Equal(0, row.GetProperty("clinics").GetInt32());
        Assert.Equal("disabled", Of(page, off).GetProperty("state").GetString());
    }

    [Fact]
    public async Task The_users_a_plan_allows_come_from_its_users_row_or_the_feature_s_baseline()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var three = await world.Factory.PutPlanAsync(grid: ("max_users", 3));
        var one = await world.Factory.PutPlanAsync(grid: ("max_users", 1));
        var unlimited = await world.Factory.PutPlanAsync(grid: ("max_users", null));
        var left = await world.Factory.PutPlanAsync();
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var page = await viewer.JsonAsync(PlansPath);
        int? Users(Guid id) =>
            Of(page, id).GetProperty("users").GetProperty("limit") is { ValueKind: JsonValueKind.Number } n ? n.GetInt32() : null;

        Assert.Equal(3, Users(three));
        Assert.Equal(1, Users(one));
        Assert.Null(Users(unlimited));

        // Not included: the feature's baseline, which for the users is none — no limit (ق-٣٨).
        Assert.Null(Users(left));
    }

    [Fact]
    public async Task With_no_clinics_the_cards_count_the_plans_and_have_nothing_else_to_count()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        await world.Factory.PutPlanAsync(disabled: true);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingManagePlans);

        var page = await viewer.JsonAsync(PlansPath);
        var cards = page.GetProperty("cards");
        Assert.Equal(Items(page).Count, cards.GetProperty("total").GetInt32());
        Assert.Equal(Items(page).Count(i => i.GetProperty("state").GetString() == "disabled"), cards.GetProperty("disabled").GetInt32());
        Assert.Equal(0, cards.GetProperty("clinics").GetInt32());
        Assert.Equal(0, cards.GetProperty("clinicsNewThisMonth").GetInt32());
        Assert.Equal(JsonValueKind.Null, cards.GetProperty("mostWanted").ValueKind);
        Assert.Equal(JsonValueKind.Null, cards.GetProperty("revenue").ValueKind);

        Assert.False(page.GetProperty("can").GetProperty("manage").GetBoolean());
        Assert.True((await manager.JsonAsync(PlansPath)).GetProperty("can").GetProperty("manage").GetBoolean());
    }

    [Fact]
    public async Task With_clinics_the_cards_and_rows_say_what_the_clinics_module_counts()
    {
        var clinics = new FakeSubscribers();
        using var world = await OpsWorld.CreateAsync(sql, configure: f => f.WithSubscribers(clinics));
        var basic = await world.Factory.PutPlanAsync(monthly: 249m);
        var pro = await world.Factory.PutPlanAsync(monthly: 499m);
        clinics.Counts[basic] = new PlanSubscriberCounts(18, 3, 2, 4482m);
        clinics.Counts[pro] = new PlanSubscriberCounts(64, 5, 4, 31936m);
        clinics.Revenue = new PlanRevenue(36418m, 32000m);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var page = await viewer.JsonAsync(PlansPath);
        var cards = page.GetProperty("cards");
        Assert.Equal(18, Of(page, basic).GetProperty("clinics").GetInt32());
        Assert.Equal(82, cards.GetProperty("clinics").GetInt32());
        Assert.Equal(8, cards.GetProperty("clinicsNewThisMonth").GetInt32());
        Assert.Equal(pro, cards.GetProperty("mostWanted").GetProperty("id").GetGuid());
        Assert.Equal(64, cards.GetProperty("mostWanted").GetProperty("clinics").GetInt32());
        Assert.Equal(36418m, cards.GetProperty("revenue").GetProperty("now").GetDecimal());
        Assert.Equal(32000m, cards.GetProperty("revenue").GetProperty("lastMonthEnd").GetDecimal());
    }

    [Fact]
    public async Task On_a_tie_the_most_wanted_plan_is_the_cheaper_then_the_newer()
    {
        var clinics = new FakeSubscribers();
        using var world = await OpsWorld.CreateAsync(sql, configure: f => f.WithSubscribers(clinics));
        var dear = await world.Factory.PutPlanAsync(monthly: 999m);
        var cheapOld = await world.Factory.PutPlanAsync(monthly: 199m);
        foreach (var plan in new[] { dear, cheapOld })
        {
            clinics.Counts[plan] = new PlanSubscriberCounts(7, 0, 0, 0m);
        }

        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);
        Assert.Equal(cheapOld, (await viewer.JsonAsync(PlansPath)).GetProperty("cards").GetProperty("mostWanted").GetProperty("id").GetGuid());

        var cheapNew = await world.Factory.PutPlanAsync(monthly: 199m);
        clinics.Counts[cheapNew] = new PlanSubscriberCounts(7, 0, 0, 0m);
        Assert.Equal(cheapNew, (await viewer.JsonAsync(PlansPath)).GetProperty("cards").GetProperty("mostWanted").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task One_plan_reads_in_full_with_what_it_includes_what_it_leaves_and_its_clinics()
    {
        var clinics = new FakeSubscribers();
        using var world = await OpsWorld.CreateAsync(sql, configure: f => f.WithSubscribers(clinics));
        var channel = await world.Factory.PutFeatureAsync();
        var quota = await world.Factory.PutFeatureAsync(kind: "limit");
        var id = await world.Factory.PutPlanAsync(grid: [(channel, null), (quota, 40), ("max_users", 10)]);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var plan = await viewer.JsonAsync($"{PlansPath}/{id}");
        foreach (var field in new[] { "description", "disabledReason", "disabledAt", "features", "excluded", "subscriptions", "clinics" })
        {
            Assert.True(plan.TryGetProperty(field, out _), field);
        }

        var included = plan.GetProperty("features").EnumerateArray()
            .ToDictionary(f => f.GetProperty("key").GetString()!, f => f.GetProperty("limit"));
        Assert.Equal(3, included.Count);
        Assert.Equal(JsonValueKind.Null, included[channel].ValueKind);
        Assert.Equal(40, included[quota].GetInt32());
        Assert.Equal(10, plan.GetProperty("users").GetProperty("limit").GetInt32());

        // What the plan leaves out of what the catalogue offers it.
        var offered = (await viewer.JsonAsync($"{PlansPath}/grid")).GetProperty("items").GetArrayLength();
        Assert.Equal(offered - 3, plan.GetProperty("excluded").GetInt32());

        // No clinic is on a plan until spec 015: nothing to count, nobody listed.
        Assert.Equal(0, plan.GetProperty("subscriptions").GetProperty("clinics").GetInt32());
        Assert.Empty(plan.GetProperty("clinics").EnumerateArray());

        clinics.Counts[id] = new PlanSubscriberCounts(64, 8, 5, 31936m);
        clinics.Clinics[id] =
        [
            new PlanClinic(Guid.NewGuid(), "عيادة النور", "Al Noor Clinic", "نشطة", "Active", DateTimeOffset.UtcNow),
            new PlanClinic(Guid.NewGuid(), "مركز الشفاء", "Al Shifa Center", "تجريبية", "Trial", DateTimeOffset.UtcNow.AddDays(-1)),
        ];
        var counted = await viewer.JsonAsync($"{PlansPath}/{id}");
        var subscriptions = counted.GetProperty("subscriptions");
        Assert.Equal((64, 31936m, 8, 5), (
            subscriptions.GetProperty("clinics").GetInt32(), subscriptions.GetProperty("monthlyRevenue").GetDecimal(),
            subscriptions.GetProperty("newThisMonth").GetInt32(), subscriptions.GetProperty("onTrial").GetInt32()));
        Assert.Equal(
            ["عيادة النور", "مركز الشفاء"],
            counted.GetProperty("clinics").EnumerateArray().Select(c => c.GetProperty("nameAr").GetString()));
    }

    [Fact]
    public async Task A_disabled_plan_reads_with_its_reason()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var id = await world.Factory.PutPlanAsync(disabled: true);
        await TestDatabase.ExecuteAsync(sql.Main.Admin, $"UPDATE [plans].[Plan] SET [DisabledReason] = N'استُبدلت' WHERE [Id] = '{id}'");
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var plan = await viewer.JsonAsync($"{PlansPath}/{id}");
        Assert.Equal("disabled", plan.GetProperty("state").GetString());
        Assert.Equal("استُبدلت", plan.GetProperty("disabledReason").GetString());
        Assert.NotEqual(JsonValueKind.Null, plan.GetProperty("disabledAt").ValueKind);
    }

    [Fact]
    public async Task An_unknown_plan_is_not_found_and_no_address_deletes_one()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var id = await world.Factory.PutPlanAsync();
        using var manager = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView, OperatorPermissions.BillingManagePlans);

        Assert.Equal((404, "NOT_FOUND"), await manager.OpsGetAsync($"{PlansPath}/{Guid.NewGuid()}").RefusalAsync());

        // There is no delete (FR-026): disabling replaces it. An address the platform does not have
        // is refused like any undeclared route (SH-056), and the plan is still there.
        using var deleting = await manager.DeleteAsync(new Uri($"{PlansPath}/{id}", UriKind.Relative));
        Assert.False(deleting.IsSuccessStatusCode, $"DELETE answered {(int)deleting.StatusCode}.");
        Assert.NotNull(await world.Factory.PlanAsync(id));
    }

    [Fact]
    public async Task Fifty_plans_answer_within_a_second()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);
        var have = Items(await viewer.JsonAsync(PlansPath)).Count;
        for (var i = have; i < 50; i++)
        {
            await world.Factory.PutPlanAsync(grid: ("max_users", 5));
        }

        // A warm-up, then the best of three: the goal is checked, and a busy machine does not fail it.
        await viewer.JsonAsync(PlansPath);
        var best = long.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var clock = Stopwatch.StartNew();
            await viewer.JsonAsync(PlansPath);
            best = Math.Min(best, clock.ElapsedMilliseconds);
        }

        Assert.True(best < 1000, $"The plans took {best} ms.");
    }
}
