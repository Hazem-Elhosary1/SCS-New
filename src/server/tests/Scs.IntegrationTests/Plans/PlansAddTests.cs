using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.FeatureFixtures;
using static Scs.IntegrationTests.Fixtures.PlanFixtures;

namespace Scs.IntegrationTests.Plans;

/// <summary>
/// Spec 013, User Story 2 (FR-010 to FR-018, FR-024; contracts/plans-api.md §2): a plan is made out
/// of the catalogue with «إدارة الباقات والاشتراكات» and by nobody else, its code derived once, its
/// grid checked whole before anything is written (ق-٤٢), and its badge moved from whoever held it.
/// Test-first: the grid's rules are plan entitlements (constitution VII).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlansAddTests(SqlServerFixture sql)
{
    private static string[] Manager { get; } = [OperatorPermissions.BillingView, OperatorPermissions.BillingManagePlans];

    private static async Task<(Guid Id, string Code)> AddedAsync(HttpClient manager, Dictionary<string, object?> body)
    {
        using var response = await manager.OpsPostAsync(PlansPath, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"POST answered {(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return (json.RootElement.GetProperty("id").GetGuid(), json.RootElement.GetProperty("code").GetString()!);
    }

    [Fact]
    public async Task Only_who_manages_plans_reads_the_grid_and_adds_a_plan()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var nobody = world.Factory.Browser();
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);
        using var manager = await world.Factory.SignInWithAsync(Manager);

        Assert.Equal(401, (await nobody.OpsPostAsync(PlansPath, NewPlan()).RefusalAsync()).Status);
        Assert.Equal((403, "PERMISSION_REQUIRED"), await viewer.OpsPostAsync(PlansPath, NewPlan()).RefusalAsync());
        Assert.Equal(200, (await viewer.OpsGetAsync($"{PlansPath}/grid").RefusalAsync()).Status);
        await AddedAsync(manager, NewPlan());
    }

    [Fact]
    public async Task The_grid_offers_the_catalogue_s_sold_features_and_names_the_badge_s_holder()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var sold = await world.Factory.PutFeatureAsync();
        var retired = await world.Factory.PutFeatureAsync(retired: true);
        var unsold = await world.Factory.PutFeatureAsync(sold: false);
        await sql.ClearBadgesAsync();
        var holder = await world.Factory.PutPlanAsync(badge: "best");
        using var viewer = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var grid = await viewer.JsonAsync($"{PlansPath}/grid");
        var keys = grid.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()).ToList();
        Assert.Contains(sold, keys);
        Assert.DoesNotContain(retired, keys);
        Assert.DoesNotContain(unsold, keys);
        Assert.DoesNotContain("core_clinic", keys);
        Assert.Contains("max_users", keys);

        var row = grid.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == sold);
        foreach (var field in new[] { "nameAr", "nameEn", "group", "kind", "unitAr", "unitEn", "status", "retired", "sold" })
        {
            Assert.True(row.TryGetProperty(field, out _), field);
        }

        Assert.Equal(holder, grid.GetProperty("badgeHolder").GetProperty("id").GetGuid());
        Assert.Equal("best", grid.GetProperty("badgeHolder").GetProperty("badge").GetString());
    }

    [Fact]
    public async Task A_plan_is_added_active_with_its_grid_and_an_empty_limit_means_no_limit()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var channel = await world.Factory.PutFeatureAsync();
        var quota = await world.Factory.PutFeatureAsync(kind: "limit");
        using var manager = await world.Factory.SignInWithAsync(Manager);

        var (id, _) = await AddedAsync(manager, NewPlan(features: [Row(channel), Row(quota), Row("max_users", 3)]));
        var plan = await world.Factory.PlanAsync(id);

        Assert.NotNull(plan);
        Assert.Equal("active", plan.State);
        Assert.Equal(499m, plan.MonthlyEgp);
        Assert.Equal(25m, plan.MonthlyUsd);
        Assert.Equal(1, plan.Version);
        Assert.Equal(3, plan.Features.Count);
        Assert.Null(plan.Features[channel]);
        Assert.Null(plan.Features[quota]);
        Assert.Equal(3, plan.Features["max_users"]);
    }

    [Fact]
    public async Task The_code_comes_from_the_english_name_and_takes_the_next_number_when_taken()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var manager = await world.Factory.SignInWithAsync(Manager);
        var name = NewName("Pro Plan");
        var expected = name.ToLowerInvariant().Replace(' ', '-');

        Assert.Equal(expected, (await AddedAsync(manager, NewPlan(name))).Code);
        Assert.Equal($"{expected}-2", (await AddedAsync(manager, NewPlan(name))).Code);
        Assert.Equal("plan", PlanCodeOf("!!!"));

        // Two adds of one English name at once both succeed, with two codes (research R3).
        var twin = NewName("Twin Plan");
        var both = await Task.WhenAll(AddedAsync(manager, NewPlan(twin)), AddedAsync(manager, NewPlan(twin)));
        Assert.Equal(2, both.Select(b => b.Code).Distinct().Count());
    }

    private static string PlanCodeOf(string nameEn) => Scs.Plans.Domain.PlanRules.BaseCode(nameEn);

    [Fact]
    public async Task Every_refusal_writes_nothing_at_all()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var channel = await world.Factory.PutFeatureAsync();
        var quota = await world.Factory.PutFeatureAsync(kind: "limit");
        var retired = await world.Factory.PutFeatureAsync(retired: true);
        var unsold = await world.Factory.PutFeatureAsync(sold: false);
        using var manager = await world.Factory.SignInWithAsync(Manager);

        (Dictionary<string, object?> Body, int Status, string Code, string? Field)[] refusals =
        [
            (NewPlan(features: [Row("no_such_feature")]), 400, "PLAN_FEATURE_UNKNOWN", null),
            (NewPlan(features: [Row(channel, 5)]), 400, "PLAN_SWITCH_HAS_LIMIT", null),
            (NewPlan(features: [Row(channel), Row(quota, 0)]), 400, "VALIDATION_FAILED", "features[1].limit"),
            (NewPlan(features: [Row(quota, 1_000_001)]), 400, "VALIDATION_FAILED", "features[0].limit"),
            (NewPlan(features: [Row(channel), Row(channel)]), 400, "VALIDATION_FAILED", "features[1].key"),
            (NewPlan(features: [Row(retired)]), 409, "PLAN_FEATURE_NOT_SOLD", null),
            (NewPlan(features: [Row(unsold)]), 409, "PLAN_FEATURE_NOT_SOLD", null),
            (NewPlan().With("nameAr", "  "), 400, "VALIDATION_FAILED", "nameAr"),
            (NewPlan().With("description", new string('و', 501)), 400, "VALIDATION_FAILED", "description"),
            (NewPlan().With("monthlyEgp", 499.999m), 400, "VALIDATION_FAILED", "monthlyEgp"),
            (NewPlan().With("annualUsd", -1m), 400, "VALIDATION_FAILED", "annualUsd"),
            (NewPlan().With("badge", "gold"), 400, "VALIDATION_FAILED", "badge"),
            (NewPlan().With("note", new string('n', 501)), 400, "VALIDATION_FAILED", "note"),
        ];

        var before = await world.Factory.PlanCountsAsync();
        foreach (var (body, status, code, field) in refusals)
        {
            var (got, gotCode, text) = await manager.OpsPostAsync(PlansPath, body).AnswerAsync();
            Assert.True((status, code) == (got, gotCode), $"{code}: answered {got} {gotCode} — {text}");
            if (field is not null)
            {
                Assert.Contains($"\"{field}\"", text, StringComparison.Ordinal);
            }
        }

        Assert.Equal(before, await world.Factory.PlanCountsAsync());
    }

    [Fact]
    public async Task Asking_for_a_badge_moves_it_from_its_holder_in_the_same_change()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        await sql.ClearBadgesAsync();
        var holder = await world.Factory.PutPlanAsync(badge: "best");
        using var manager = await world.Factory.SignInWithAsync(Manager);

        var (id, _) = await AddedAsync(manager, NewPlan(badge: "best_selling"));

        Assert.Equal("best_selling", (await world.Factory.PlanAsync(id))!.Badge);
        var former = await world.Factory.PlanAsync(holder);
        Assert.Null(former!.Badge);
        Assert.Equal(2, former.Version);

        // The former holder's own history says why (research R9).
        var taken = Assert.Single(await sql.PlanEntriesAsync(former.Code, "plan.updated"));
        Assert.Contains("badge", taken.Changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_adds_asking_for_a_badge_at_once_both_succeed_and_one_plan_holds_it()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        await sql.ClearBadgesAsync();
        using var manager = await world.Factory.SignInWithAsync(Manager);

        var both = await Task.WhenAll(
            AddedAsync(manager, NewPlan(badge: "best")),
            AddedAsync(manager, NewPlan(badge: "most_popular")));

        var badges = new List<string?>();
        foreach (var (id, _) in both)
        {
            badges.Add((await world.Factory.PlanAsync(id))!.Badge);
        }

        Assert.Single(badges, b => b is not null);
        var holders = await TestDatabase.ReadColumnAsync(sql.Main.Admin, "SELECT CAST(COUNT(*) AS nvarchar(10)) FROM [plans].[Plan] WHERE [Badge] IS NOT NULL");
        Assert.Equal("1", holders.Single());
    }

    [Fact]
    public async Task The_add_is_in_the_platform_s_trail_with_every_field_the_grid_and_the_note()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var channel = await world.Factory.PutFeatureAsync();
        using var manager = await world.Factory.SignInWithAsync(Manager);
        var (_, code) = await AddedAsync(manager, NewPlan(features: [Row(channel), Row("max_users", 3)]).With("note", "باقة الافتتاح"));

        var entry = Assert.Single(await sql.PlanEntriesAsync(code, "plan.created"));
        Assert.Equal("operator", entry.ActorKind);
        Assert.Equal("باقة تجريبية", entry.TargetName);
        Assert.Contains("باقة الافتتاح", entry.Facts, StringComparison.Ordinal);
        Assert.DoesNotContain("\"key\"", entry.Facts, StringComparison.Ordinal);
        foreach (var field in new[] { "nameAr", "nameEn", "description", "monthlyEgp", "annualEgp", "monthlyUsd", "annualUsd", $"feature:{channel}", "feature:max_users" })
        {
            Assert.Contains($"\"{field}\"", entry.Changes, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_clinic_s_session_adds_no_plan()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var clinic = await sql.Main.SeedClinicAsync();
        var (_, staffEmail) = await world.Factory.CreateAccountAsync(clinic);
        using var staff = await world.Factory.SignInAsAsync(staffEmail, SignIn.Password);
        Assert.Equal(401, (await staff.OpsPostAsync(PlansPath, NewPlan()).RefusalAsync()).Status);
    }
}
