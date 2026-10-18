using System.Diagnostics;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using static Scs.IntegrationTests.Fixtures.ClinicFixtures;

namespace Scs.IntegrationTests.Clinics;

/// <summary>
/// Spec 015, User Story 2 (FR-007 – FR-013; SC-004, SC-007): the operators' list — the cards and
/// their sum, the search on five fields, the filters and their facets, the sort with no date last,
/// the pages — and clinics being set up or discarded never in it. Every database test shares one
/// database, so each test finds its own clinics by a name nobody else uses.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClinicsListTests(SqlServerFixture sql)
{
    private static string Tag() => $"tag{Guid.NewGuid():N}"[..14];

    private static List<JsonElement> Items(JsonElement page) => [.. page.GetProperty("items").EnumerateArray()];

    [Fact]
    public async Task The_cards_add_up_to_the_total_and_agree_with_the_badges()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        await editor.AddClinicAsync(NewClinic(plan));

        var page = await editor.JsonAsync($"{ClinicsPath}?size=50");
        var cards = page.GetProperty("cards");
        var follow = cards.GetProperty("needsFollowUp");
        var parts = cards.GetProperty("active").GetInt32() + cards.GetProperty("trial").GetInt32()
                    + follow.GetProperty("overdue").GetInt32() + follow.GetProperty("readOnly").GetInt32() + follow.GetProperty("suspended").GetInt32();

        Assert.Equal(cards.GetProperty("total").GetInt32(), parts);
        Assert.Equal(cards.GetProperty("total").GetInt32(), page.GetProperty("total").GetInt32());
        Assert.True(cards.GetProperty("newThisMonth").GetInt32() >= 1);
        Assert.True(cards.GetProperty("trial").GetInt32() >= 1);
    }

    [Fact]
    public async Task The_search_matches_each_name_and_the_owner_in_any_case()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var tag = Tag();
        var email = $"{tag}@clinic.example";
        var body = NewClinic(plan, $"Clinic {tag}", email).With("nameAr", $"عيادة {tag}").With("ownerName", $"مالك {tag}").With("mobile", "+20 1557 000 111");
        var (id, _) = await editor.AddClinicAsync(body);

        foreach (var search in new[] { tag.ToUpperInvariant(), $"عيادة {tag}", $"مالك {tag}", email, "1557000111", "1557-000" })
        {
            var page = await editor.JsonAsync($"{ClinicsPath}?search={Uri.EscapeDataString(search)}");
            Assert.Contains(Items(page), row => row.GetProperty("id").GetGuid() == id);
        }

        var none = await editor.JsonAsync($"{ClinicsPath}?search={Uri.EscapeDataString(Tag())}");
        Assert.Equal(0, none.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task The_filters_narrow_and_the_facets_offer_what_the_clinics_hold()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var first = await world.Factory.PutPlanAsync();
        var second = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var tag = Tag();
        var (a, _) = await editor.AddClinicAsync(NewClinic(first, $"A {tag}"));
        var (b, _) = await editor.AddClinicAsync(NewClinic(second, $"B {tag}"));
        var search = $"search={tag}";

        var both = await editor.JsonAsync($"{ClinicsPath}?{search}");
        Assert.Equal(2, both.GetProperty("total").GetInt32());
        var plans = both.GetProperty("facets").GetProperty("plans").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(new[] { first, second }.Order(), plans.Order());
        Assert.Equal(["trial"], both.GetProperty("facets").GetProperty("statuses").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(["trial"], both.GetProperty("facets").GetProperty("lengths").EnumerateArray().Select(s => s.GetString()));
        // Only the windows some searched clinic ends in: a 14-day trial is in 14 and 30, not 7 (FR-010; T083).
        Assert.Equal([14, 30], both.GetProperty("facets").GetProperty("endsWithin").EnumerateArray().Select(d => d.GetInt32()));
        var chosen = await editor.JsonAsync($"{ClinicsPath}?{search}&endsWithin=7");
        Assert.Equal([7, 14, 30], chosen.GetProperty("facets").GetProperty("endsWithin").EnumerateArray().Select(d => d.GetInt32()));

        var onFirst = await editor.JsonAsync($"{ClinicsPath}?{search}&plan={first}");
        Assert.Equal([a], Items(onFirst).Select(r => r.GetProperty("id").GetGuid()));

        Assert.Equal(2, (await editor.JsonAsync($"{ClinicsPath}?{search}&status=trial&length=trial&endsWithin=30")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await editor.JsonAsync($"{ClinicsPath}?{search}&status=active")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await editor.JsonAsync($"{ClinicsPath}?{search}&endsWithin=7")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await editor.JsonAsync($"{ClinicsPath}?{search}&length=monthly")).GetProperty("total").GetInt32());

        // A chosen plan is still offered when the search leaves none of its clinics, so it can be cleared.
        var gone = await editor.JsonAsync($"{ClinicsPath}?search={Tag()}&plan={second}");
        Assert.Contains(gone.GetProperty("facets").GetProperty("plans").EnumerateArray(), p => p.GetProperty("id").GetGuid() == second);
        Assert.NotEqual(b, Guid.Empty);
    }

    [Fact]
    public async Task The_ends_column_sorts_both_ways_with_no_date_last_and_ties_newest_first()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var tag = Tag();
        var (early, _) = await editor.AddClinicAsync(NewClinic(plan, $"Early {tag}"));
        var (late, _) = await editor.AddClinicAsync(NewClinic(plan, $"Late {tag}"));
        await sql.ExecuteAsync(sql.Main.Crossing, $"UPDATE subscriptions.ClinicPlan SET TrialEndsAtUtc = DATEADD(day, 3, SYSDATETIMEOFFSET()) WHERE ClinicId = '{early}'");
        var (ended, _) = await editor.AddClinicAsync(NewClinic(plan, $"Ended {tag}"));
        await sql.ExecuteAsync(sql.Main.Crossing, $"UPDATE subscriptions.ClinicPlan SET TrialEndsAtUtc = DATEADD(day, -1, SYSDATETIMEOFFSET()) WHERE ClinicId = '{ended}'");

        Guid[] Order(JsonElement page) => [.. Items(page).Select(r => r.GetProperty("id").GetGuid())];
        Assert.Equal([early, late, ended], Order(await editor.JsonAsync($"{ClinicsPath}?search={tag}&sort=ends-asc")));
        Assert.Equal([late, early, ended], Order(await editor.JsonAsync($"{ClinicsPath}?search={tag}&sort=ends-desc")));
        Assert.Equal([ended, late, early], Order(await editor.JsonAsync($"{ClinicsPath}?search={tag}")));

        var row = Items(await editor.JsonAsync($"{ClinicsPath}?search=Ended%20{tag}")).Single();
        Assert.Equal("active", row.GetProperty("status").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("endsAt").ValueKind);
    }

    [Fact]
    public async Task The_list_pages_by_eight_and_takes_no_more_than_fifty()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var tag = Tag();
        for (var i = 0; i < 9; i++)
        {
            await editor.AddClinicAsync(NewClinic(plan, $"Page {i} {tag}"));
        }

        var first = await editor.JsonAsync($"{ClinicsPath}?search={tag}");
        var second = await editor.JsonAsync($"{ClinicsPath}?search={tag}&page=2");
        Assert.Equal((8, 9, 8), (Items(first).Count, first.GetProperty("total").GetInt32(), first.GetProperty("size").GetInt32()));
        Assert.Single(Items(second));
        Assert.Equal(50, (await editor.JsonAsync($"{ClinicsPath}?size=500")).GetProperty("size").GetInt32());
        Assert.Equal(2, (await editor.JsonAsync($"{ClinicsPath}?search={tag}&page=99")).GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task Clinics_being_set_up_or_discarded_are_never_listed_and_the_actions_follow_the_permission()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        using var viewer = await world.Factory.SignInWithAsync(Viewer);
        var tag = Tag();
        var (hidden, _) = await editor.AddClinicAsync(NewClinic(plan, $"Hidden {tag}"));
        await sql.ExecuteAsync(sql.Main.Crossing, $"UPDATE tenancy.Clinic SET State = 'setting-up' WHERE Id = '{hidden}'");

        Assert.Equal(0, (await editor.JsonAsync($"{ClinicsPath}?search={tag}")).GetProperty("total").GetInt32());
        Assert.True((await editor.JsonAsync(ClinicsPath)).GetProperty("can").GetProperty("add").GetBoolean());
        Assert.False((await viewer.JsonAsync(ClinicsPath)).GetProperty("can").GetProperty("add").GetBoolean());
    }

    [Fact]
    public async Task A_search_over_three_hundred_clinics_answers_in_under_a_second()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var visible = (await editor.JsonAsync(ClinicsPath)).GetProperty("total").GetInt32();
        for (var i = visible; i < 300; i++)
        {
            // Listed clinics have a current short name: the seed's raw clinic, with its directory entry.
            var id = await sql.Main.SeedClinicAsync();
            await sql.ExecuteAsync(
                sql.Main.Crossing,
                $"INSERT INTO tenancy.ClinicDirectory (ShortName, ClinicId, IsCurrent, AssignedAtUtc) VALUES ('load-{id:N}', '{id}', 1, SYSDATETIMEOFFSET())");
        }

        await editor.JsonAsync(ClinicsPath);
        var times = new List<double>();
        // Fifty searches, as T040 says (T098).
        for (var i = 0; i < 50; i++)
        {
            var clock = Stopwatch.StartNew();
            await editor.JsonAsync($"{ClinicsPath}?search=clinic&status=trial&plan={plan}&sort=ends-asc");
            times.Add(clock.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        var slowest = times[^1];
        Console.WriteLine($"Clinics list over {Math.Max(visible, 300)} clinics: median {times[times.Count / 2]:0} ms, slowest {slowest:0} ms");
        Assert.True(slowest < 1000, $"The slowest search took {slowest:0} ms.");
    }
}
