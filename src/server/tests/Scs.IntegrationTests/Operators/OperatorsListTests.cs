using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.Operators.Domain;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, User Story 3 (FR-022 to FR-024; SC-008): the team in one list — newest first,
/// searched, filtered and paged, with its cards and the values its filters may offer.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorsListTests(SqlServerFixture sql)
{
    private static List<JsonElement> Items(JsonElement page) => [.. page.GetProperty("items").EnumerateArray()];

    private async Task<int> CountAsync(string where = "1 = 1") =>
        int.Parse((await sql.OpsColumnAsync($"SELECT COUNT(*) FROM [ops].[Operator] WHERE {where}")).Single(), System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Only_who_may_see_the_team_reads_it()
    {
        using var world = await OpsWorld.CreateAsync(sql);

        using var nobody = world.Factory.Browser();
        using var anonymous = await nobody.OpsGetAsync(TeamHelp.Operators);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var (_, without, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.TicketsView);
        using var other = await world.Factory.SignInOperatorAsync(without);
        var (status, code, _) = await other.OpsGetAsync(TeamHelp.Operators).AnswerAsync();
        Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));

        var (_, viewerEmail) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        using var viewer = await world.Factory.SignInOperatorAsync(viewerEmail);
        using var seen = await viewer.OpsGetAsync(TeamHelp.Operators);
        Assert.Equal(HttpStatusCode.OK, seen.StatusCode);
    }

    [Fact]
    public async Task The_list_is_newest_first_eight_to_a_page_and_searched_by_part_of_a_name_or_an_email()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var mark = $"q{Guid.NewGuid():N}"[..12];

        var made = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            world.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
            var (id, _) = await world.Factory.CreateOperatorAsync(email: $"{mark}-{i}@heta.example");
            made.Add(id);
        }

        var first = await admin.JsonAsync($"{TeamHelp.Operators}?search={mark.ToUpperInvariant()}");
        Assert.Equal((10, 1, 8), (first.GetProperty("total").GetInt32(), first.GetProperty("page").GetInt32(), first.GetProperty("pageSize").GetInt32()));
        Assert.Equal(made.AsEnumerable().Reverse().Take(8), Items(first).Select(row => row.GetProperty("id").GetGuid()));

        var second = await admin.JsonAsync($"{TeamHelp.Operators}?search={mark}&page=2");
        Assert.Equal(made.Take(2).Reverse(), Items(second).Select(row => row.GetProperty("id").GetGuid()));

        // A page past the end is the last one; a wildcard is a letter like any other.
        Assert.Equal(2, (await admin.JsonAsync($"{TeamHelp.Operators}?search={mark}&page=99")).GetProperty("page").GetInt32());
        Assert.Equal(0, (await admin.JsonAsync($"{TeamHelp.Operators}?search={mark}%25zz")).GetProperty("total").GetInt32());

        // By part of the name.
        await world.Factory.OpsSqlAsync($"UPDATE [ops].[Operator] SET [FullName] = N'ليلى {mark} حسن' WHERE [Id] = '{made[3]}'");
        var byName = await admin.JsonAsync($"{TeamHelp.Operators}?search={Uri.EscapeDataString($"لى {mark} ح")}");
        Assert.Equal(made[3], Assert.Single(Items(byName)).GetProperty("id").GetGuid());

        // A row carries what the list shows, and nothing that opens an account.
        var row = Items(first)[0];
        Assert.Equal(
            ["email", "fullName", "id", "lastSignInAtUtc", "role", "state", "twoStep"],
            row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("super-admin", row.GetProperty("role").GetProperty("systemKey").GetString());
        Assert.DoesNotContain("token", first.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", first.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("locked", first.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task State_and_role_filter_and_the_cards_and_filter_values_are_about_the_whole_team()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var mark = $"f{Guid.NewGuid():N}"[..12];
        var role = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsReply);

        var (active, _) = await world.Factory.CreateOperatorAsync(email: $"{mark}-a@heta.example", roleId: role);
        var (invited, _) = await world.Factory.CreateOperatorAsync(OperatorStates.Invited, $"{mark}-i@heta.example", roleId: role);
        var (suspended, _) = await world.Factory.CreateOperatorAsync(OperatorStates.Suspended, $"{mark}-s@heta.example");
        var unheld = await world.Factory.CreateRoleAsync(OperatorPermissions.BillingView);

        async Task<List<Guid>> IdsAsync(string query) =>
            [.. Items(await admin.JsonAsync($"{TeamHelp.Operators}?search={mark}&{query}")).Select(r => r.GetProperty("id").GetGuid())];

        Assert.Equal([invited], await IdsAsync("state=invited"));
        Assert.Equal([suspended], await IdsAsync("state=suspended"));
        Assert.Equal([invited, active], await IdsAsync($"roleId={role}"));
        Assert.Equal([active], await IdsAsync($"roleId={role}&state=active"));
        Assert.Equal(3, (await IdsAsync("state=no-such-state")).Count);

        // The cards do not follow the search: they count everyone.
        var page = await admin.JsonAsync($"{TeamHelp.Operators}?search={mark}");
        var cards = page.GetProperty("cards");
        Assert.Equal(await CountAsync(), cards.GetProperty("total").GetInt32());
        Assert.Equal(await CountAsync("State = 'active'"), cards.GetProperty("active").GetInt32());
        Assert.Equal(await CountAsync("State = 'invited'"), cards.GetProperty("invited").GetInt32());
        Assert.Equal(await CountAsync("TwoStepRequired = 1"), cards.GetProperty("twoStepOn").GetInt32());

        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var local = TimeZoneInfo.ConvertTime(world.Factory.Clock.GetUtcNow(), cairo);
        var monthStart = new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, cairo.GetUtcOffset(new DateTime(local.Year, local.Month, 1)));
        Assert.Equal(
            await CountAsync($"ActivatedAtUtc >= '{monthStart:yyyy-MM-ddTHH:mm:sszzz}'"),
            cards.GetProperty("activatedThisMonth").GetInt32());

        // The filter offers the roles someone holds, and not the one nobody does.
        var filters = page.GetProperty("filters");
        var offered = filters.GetProperty("roles").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(role, offered);
        Assert.Contains(TeamHelp.SuperAdmin, offered);
        Assert.DoesNotContain(unheld, offered);
        Assert.Equal(["active", "invited", "suspended"], filters.GetProperty("states").EnumerateArray().Select(s => s.GetString()));
    }

    [Fact]
    public async Task Two_hundred_operators_answer_within_a_second()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var have = await CountAsync();

        if (have < 200)
        {
            await world.Factory.OpsSqlAsync(
                $"""
                 INSERT INTO [ops].[Operator] (Id, FullName, Email, Language, State, FailedAttempts, SpentCodes, CreatedAtUtc, RoleId, TwoStepRequired)
                 SELECT TOP ({200 - have}) NEWID(), N'مشغل للقياس', CONCAT('load-', NEWID(), '@heta.example'), 'ar', 'invited', 0, 0,
                        SYSDATETIMEOFFSET(), '{TeamHelp.Viewer}', 1
                 FROM sys.all_objects
                 """);
        }

        _ = await admin.JsonAsync(TeamHelp.Operators);
        var watch = Stopwatch.StartNew();
        var page = await admin.JsonAsync($"{TeamHelp.Operators}?search=heta&page=3");
        watch.Stop();

        Assert.True(page.GetProperty("total").GetInt32() >= 200);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"The list took {watch.ElapsedMilliseconds} ms.");
    }
}
