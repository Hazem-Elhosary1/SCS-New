using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, User Story 6 (FR-010 to FR-021): roles are made from the catalogue, within the
/// guards — the platform's own three are never written, nobody adds what they do not hold,
/// nobody edits the role they hold, and a held role is not deleted.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RolesTests(SqlServerFixture sql)
{
    private static readonly string[] WithReply = ["billing.view", "tickets.view", "tickets.reply"];
    private static readonly string[] WithPlans = ["billing.view", "billing.managePlans", "tickets.view"];
    private static readonly string[] Reply = ["tickets.view", "tickets.reply"];
    private static readonly string[] SeesTheTeam = ["operators.view"];

    private static string Path(Guid id) => $"{TeamHelp.Roles}/{id}";

    private static object NewRole(string? name = null, params string[] codes) => new
    {
        name = name ?? $"دور {Guid.NewGuid():N}"[..20],
        description = "يرد على التذاكر",
        codes = codes.Length == 0 ? [OperatorPermissions.TicketsReply] : codes,
    };

    private static Guid IdOf(string text) => JsonDocument.Parse(text).RootElement.GetProperty("id").GetGuid();

    private static List<string> Codes(JsonElement role) => [.. role.GetProperty("codes").EnumerateArray().Select(c => c.GetString()!)];

    private static async Task<Dictionary<string, object?>> EditBodyAsync(HttpClient browser, Guid id)
    {
        var role = await browser.JsonAsync(Path(id));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = role.GetProperty("name").GetString(),
            ["description"] = role.GetProperty("description").GetString(),
            ["codes"] = Codes(role),
            ["version"] = role.GetProperty("version").GetString(),
        };
    }

    [Fact]
    public async Task Who_sees_the_team_reads_the_list_and_only_who_manages_roles_reads_the_form_or_writes()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var role = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsView);

        var (_, viewerEmail) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        using var viewer = await world.Factory.SignInOperatorAsync(viewerEmail);
        using var list = await viewer.OpsGetAsync(TeamHelp.Roles);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        // Managing operators is not managing roles.
        var (_, managerEmail, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.OperatorsManage);
        using var manager = await world.Factory.SignInOperatorAsync(managerEmail);

        foreach (var browser in new[] { viewer, manager })
        {
            foreach (var read in new[] { Path(role), "/v1/ops/permissions" })
            {
                var (status, code, _) = await browser.OpsGetAsync(read).AnswerAsync();
                Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));
            }

            Assert.Equal(403, (await browser.OpsPostAsync(TeamHelp.Roles, NewRole()).AnswerAsync()).Status);
            Assert.Equal(403, (await browser.OpsPutAsync(Path(role), NewRole()).AnswerAsync()).Status);
            Assert.Equal(403, (await browser.OpsPostAsync($"{Path(role)}/delete", new { }).AnswerAsync()).Status);
        }

        var (_, withoutEmail, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.TicketsView);
        using var without = await world.Factory.SignInOperatorAsync(withoutEmail);
        Assert.Equal(403, (await without.OpsGetAsync(TeamHelp.Roles).AnswerAsync()).Status);

        using var nobody = world.Factory.Browser();
        using var anonymous = await nobody.OpsGetAsync(TeamHelp.Roles);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task The_list_has_the_system_roles_first_then_custom_ones_oldest_first_with_their_counts()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        var held = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsReply, OperatorPermissions.BillingView);
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        var unheld = await world.Factory.CreateRoleAsync(OperatorPermissions.SettingsEdit);
        await world.Factory.CreateOperatorAsync(roleId: held);
        await world.Factory.CreateOperatorAsync(Scs.Operators.Domain.OperatorStates.Suspended, roleId: held);

        var list = await admin.JsonAsync(TeamHelp.Roles);
        var items = list.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(["super-admin", "billing-admin", "viewer"], items.Take(3).Select(r => r.GetProperty("systemKey").GetString()));
        Assert.All(items.Skip(3), r => Assert.Equal(JsonValueKind.Null, r.GetProperty("systemKey").ValueKind));
        Assert.Equal([28, 11, 11], items.Take(3).Select(r => r.GetProperty("permissions").GetInt32()));
        Assert.All(items.Take(3), r => Assert.Equal((false, false), (r.GetProperty("can").GetProperty("edit").GetBoolean(), r.GetProperty("can").GetProperty("delete").GetBoolean())));

        var ids = items.Select(r => r.GetProperty("id").GetGuid()).ToList();
        Assert.True(ids.IndexOf(held) < ids.IndexOf(unheld));
        Assert.Equal(items.Count - 2, ids.IndexOf(held));

        var heldRow = items[ids.IndexOf(held)];
        Assert.Equal(
            (2, true, 3, true, true),
            (heldRow.GetProperty("operators").GetInt32(), heldRow.GetProperty("inUse").GetBoolean(), heldRow.GetProperty("permissions").GetInt32(),
                heldRow.GetProperty("can").GetProperty("edit").GetBoolean(), heldRow.GetProperty("can").GetProperty("delete").GetBoolean()));
        var unheldRow = items[ids.IndexOf(unheld)];
        Assert.Equal((0, false, 2), (unheldRow.GetProperty("operators").GetInt32(), unheldRow.GetProperty("inUse").GetBoolean(), unheldRow.GetProperty("permissions").GetInt32()));

        var cards = list.GetProperty("cards");
        Assert.Equal(
            (items.Count, items.Count(r => r.GetProperty("inUse").GetBoolean()), items.Count - 3, 3),
            (cards.GetProperty("total").GetInt32(), cards.GetProperty("inUse").GetInt32(), cards.GetProperty("custom").GetInt32(), cards.GetProperty("system").GetInt32()));

        // Whoever only sees the team is offered nothing to do.
        var (_, viewerEmail) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        using var viewer = await world.Factory.SignInOperatorAsync(viewerEmail);
        Assert.All(
            (await viewer.JsonAsync(TeamHelp.Roles)).GetProperty("items").EnumerateArray(),
            r => Assert.False(r.GetProperty("can").GetProperty("edit").GetBoolean()));
    }

    [Fact]
    public async Task A_role_is_created_with_what_its_permissions_need_and_recorded()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var name = $"  وكيل دعم {Guid.NewGuid():N}"[..24];

        var (status, _, text) = await admin.OpsPostAsync(
            TeamHelp.Roles, NewRole(name + "  ", OperatorPermissions.NoticesSend, OperatorPermissions.TicketsViewAll)).AnswerAsync();
        Assert.Equal(201, status);
        var id = IdOf(text);

        var role = await admin.JsonAsync(Path(id));
        Assert.Equal(name.Trim(), role.GetProperty("name").GetString());
        Assert.Equal(["clinics.view", "tickets.view", "tickets.viewAll", "notices.send"], Codes(role));
        Assert.Equal(JsonValueKind.Null, role.GetProperty("readOnlyReason").ValueKind);

        var entry = Assert.Single(await sql.PlatformEntriesAsync(id, "opsrole.created"));
        Assert.Equal((world.OperatorId, name.Trim()), (entry.Actor!.Value, entry.TargetName));
        Assert.Contains("\"added\":[\"clinics.view\",\"tickets.view\",\"tickets.viewAll\",\"notices.send\"]", entry.Changes, StringComparison.Ordinal);

        // The name is one role's, whatever its case or the spaces around it; and never a system role's.
        foreach (var taken in new[] { name.Trim().ToUpperInvariant(), $" {name.Trim()} ", "مسؤول عام", "viewer only", "Super Admin" })
        {
            var (again, code, _) = await admin.OpsPostAsync(TeamHelp.Roles, NewRole(taken)).AnswerAsync();
            Assert.Equal((409, "OPS_ROLE_NAME_TAKEN"), (again, code));
        }
    }

    [Theory]
    [InlineData("name", "", "REQUIRED")]
    [InlineData("name", "101", "TOO_LONG")]
    [InlineData("description", "501", "TOO_LONG")]
    [InlineData("codes", "none", "REQUIRED")]
    [InlineData("codes", "tickets.delete", "INVALID")]
    public async Task The_limits_of_a_role_are_refused_naming_their_field(string field, string value, string expected)
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = $"دور {Guid.NewGuid():N}"[..20],
            ["description"] = null,
            ["codes"] = new[] { OperatorPermissions.TicketsView },
        };
        body[field] = field == "codes"
            ? value == "none" ? [] : new[] { value }
            : int.TryParse(value, out var length) ? new string('n', length) : value;

        var (status, code, text) = await admin.OpsPostAsync(TeamHelp.Roles, body).AnswerAsync();

        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        using var answer = JsonDocument.Parse(text);
        Assert.Equal(expected, answer.RootElement.GetProperty("errors").GetProperty(field)[0].GetString());
    }

    [Fact]
    public async Task Nobody_adds_to_a_role_what_they_do_not_hold_but_what_it_already_had_stays()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (_, makerEmail, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.OperatorsManageRoles, OperatorPermissions.TicketsReply);
        using var maker = await world.Factory.SignInOperatorAsync(makerEmail);

        // Creating: the codes they lack are named.
        var (status, code, text) = await maker.OpsPostAsync(
            TeamHelp.Roles, NewRole(null, OperatorPermissions.TicketsReply, OperatorPermissions.SettingsEdit)).AnswerAsync();
        Assert.Equal((403, "OPS_PERMISSIONS_NOT_HELD"), (status, code));
        using (var refused = JsonDocument.Parse(text))
        {
            Assert.Equal(["settings.view", "settings.edit"], refused.RootElement.GetProperty("permissions").EnumerateArray().Select(c => c.GetString()));
        }

        Assert.Equal(201, (await maker.OpsPostAsync(TeamHelp.Roles, NewRole(null, OperatorPermissions.TicketsReply)).AnswerAsync()).Status);

        // Editing a role that holds more than they do: what it had stays, and nothing more of it is added.
        var wide = await world.Factory.CreateRoleAsync(OperatorPermissions.BillingView, OperatorPermissions.TicketsView);
        var body = await EditBodyAsync(maker, wide);
        Assert.Equal(204, (await maker.OpsPutAsync(Path(wide), body.With("codes", WithReply)).AnswerAsync()).Status);
        Assert.Equal(["tickets.view", "tickets.reply", "billing.view"], Codes(await admin.JsonAsync(Path(wide))));

        body = await EditBodyAsync(maker, wide);
        var (more, moreCode, _) = await maker.OpsPutAsync(
            Path(wide), body.With("codes", WithPlans)).AnswerAsync();
        Assert.Equal((403, "OPS_PERMISSIONS_NOT_HELD"), (more, moreCode));

        // The catalogue says what they may tick.
        var catalogue = (await maker.JsonAsync("/v1/ops/permissions")).GetProperty("groups").EnumerateArray().ToList();
        Assert.Equal(OperatorPermissions.Groups.All, catalogue.Select(g => g.GetProperty("key").GetString()));
        var all = catalogue.SelectMany(g => g.GetProperty("items").EnumerateArray()).ToList();
        Assert.Equal(28, all.Count);
        Assert.Equal(
            ["tickets.view", "tickets.reply", "operators.view", "operators.manageRoles"],
            all.Where(p => p.GetProperty("mayGrant").GetBoolean()).Select(p => p.GetProperty("code").GetString()));
        Assert.Equal("clinics.view", all.Single(p => p.GetProperty("code").GetString() == "notices.send").GetProperty("needs").GetString());
        Assert.All((await admin.JsonAsync("/v1/ops/permissions")).GetProperty("groups").EnumerateArray()
            .SelectMany(g => g.GetProperty("items").EnumerateArray()), p => Assert.True(p.GetProperty("mayGrant").GetBoolean()));
    }

    [Fact]
    public async Task Basing_a_role_on_another_takes_what_the_maker_holds_and_says_what_was_left()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (_, makerEmail, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.OperatorsManageRoles, OperatorPermissions.TicketsReply);
        using var maker = await world.Factory.SignInOperatorAsync(makerEmail);
        var source = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsReply, OperatorPermissions.ClinicsEdit);

        var copy = (await maker.JsonAsync(Path(source))).GetProperty("copy");
        Assert.Equal(["tickets.view", "tickets.reply"], copy.GetProperty("codes").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(["clinics.view", "clinics.edit"], copy.GetProperty("left").EnumerateArray().Select(c => c.GetString()));

        var whole = (await admin.JsonAsync(Path(source))).GetProperty("copy");
        Assert.Equal(4, whole.GetProperty("codes").GetArrayLength());
        Assert.Equal(0, whole.GetProperty("left").GetArrayLength());

        var (status, _, text) = await maker.OpsPostAsync(
            TeamHelp.Roles,
            new { name = $"نسخة {Guid.NewGuid():N}"[..20], codes = Reply, basedOnId = source }).AnswerAsync();
        Assert.Equal(201, status);
        Assert.Contains("\"basedOn\":\"Role ", Assert.Single(await sql.PlatformEntriesAsync(IdOf(text), "opsrole.created")).Facts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editing_a_role_reaches_its_holders_at_their_next_request_and_signs_nobody_out()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (holder, holderEmail, role) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.TicketsView);
        using var theirs = await world.Factory.SignInOperatorAsync(holderEmail);
        Assert.Equal(403, (await theirs.OpsGetAsync(TeamHelp.Operators).AnswerAsync()).Status);

        var body = await EditBodyAsync(admin, role);
        var renamed = $"فريق {Guid.NewGuid():N}"[..20];
        Assert.Equal(204, (await admin.OpsPutAsync(
            Path(role),
            body.With("name", renamed).With("description", "يرى الفريق").With("codes", SeesTheTeam).With("note", "توسيع")).AnswerAsync()).Status);

        using var seen = await theirs.OpsGetAsync(TeamHelp.Operators);
        Assert.Equal(HttpStatusCode.OK, seen.StatusCode);
        Assert.Equal(1, await world.Factory.OpsAsync(db => Task.FromResult(db.Sessions.Count(s => s.OperatorId == holder && s.EndedAtUtc == null))));

        var entry = Assert.Single(await sql.PlatformEntriesAsync(role, "opsrole.edited"));
        Assert.Contains("\"added\":[\"operators.view\"],\"removed\":[\"tickets.view\"]", entry.Changes, StringComparison.Ordinal);
        Assert.Contains($"\"after\":\"{renamed}\"", entry.Changes, StringComparison.Ordinal);
        Assert.Contains("\"note\":\"توسيع\"", entry.Facts, StringComparison.Ordinal);

        // The form still holds the old version.
        var (stale, staleCode, _) = await admin.OpsPutAsync(Path(role), body.With("name", renamed + "٢")).AnswerAsync();
        Assert.Equal((409, "OPS_CHANGED_ELSEWHERE"), (stale, staleCode));

        // Another role's name is not taken; its own is kept.
        var other = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsView);
        var otherName = (await admin.JsonAsync(Path(other))).GetProperty("name").GetString();
        body = await EditBodyAsync(admin, role);
        Assert.Equal((409, "OPS_ROLE_NAME_TAKEN"), await admin.OpsPutAsync(Path(role), body.With("name", otherName)).RefusalAsync());
        Assert.Equal(204, (await admin.OpsPutAsync(Path(role), body.With("description", null)).AnswerAsync()).Status);
    }

    [Fact]
    public async Task A_system_role_is_never_written_and_nobody_but_a_super_admin_edits_the_role_they_hold()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        var system = await admin.JsonAsync(Path(TeamHelp.Viewer));
        Assert.Equal("system", system.GetProperty("readOnlyReason").GetString());
        var body = new { name = "أي اسم", codes = new[] { "tickets.view" }, version = system.GetProperty("version").GetString() };
        Assert.Equal((403, "OPS_SYSTEM_ROLE"), await admin.OpsPutAsync(Path(TeamHelp.Viewer), body).RefusalAsync());
        Assert.Equal((403, "OPS_SYSTEM_ROLE"), await admin.OpsPostAsync($"{Path(TeamHelp.SuperAdmin)}/delete", new { }).RefusalAsync());
        Assert.Equal(11, Codes(await admin.JsonAsync(Path(TeamHelp.Viewer))).Count);

        var (_, makerEmail, own) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.OperatorsManageRoles);
        using var maker = await world.Factory.SignInOperatorAsync(makerEmail);
        var theirs = await maker.JsonAsync(Path(own));
        Assert.Equal("own-role", theirs.GetProperty("readOnlyReason").GetString());
        var (status, code, _) = await maker.OpsPutAsync(Path(own), await EditBodyAsync(maker, own)).AnswerAsync();
        Assert.Equal((403, "OPS_OWN_ROLE_EDIT"), (status, code));

        // A super admin may edit that role; and one that holds everything is still not the super admin's.
        var everything = (await EditBodyAsync(admin, own)).With("codes", OperatorPermissions.All.Select(p => p.Code).ToArray());
        Assert.Equal(204, (await admin.OpsPutAsync(Path(own), everything).AnswerAsync()).Status);
        var session = await maker.JsonAsync("/v1/ops/auth/session");
        Assert.Equal(28, session.GetProperty("permissions").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, session.GetProperty("operator").GetProperty("role").GetProperty("systemKey").ValueKind);
        Assert.Equal(
            (403, "OPS_ROLE_NOT_GRANTABLE"),
            await maker.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(TeamHelp.SuperAdmin)).RefusalAsync());
        Assert.Equal((403, "OPS_OWN_ROLE_EDIT"), await maker.OpsPutAsync(Path(own), await EditBodyAsync(maker, own)).RefusalAsync());
    }

    [Fact]
    public async Task A_held_role_is_not_deleted_and_a_deleted_one_is_hidden_but_keeps_its_name_in_the_trail()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var role = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsView);
        var name = (await admin.JsonAsync(Path(role))).GetProperty("name").GetString()!;
        var (holder, _) = await world.Factory.CreateOperatorAsync(roleId: role);
        await world.Factory.CreateOperatorAsync(Scs.Operators.Domain.OperatorStates.Invited, roleId: role);

        var (status, code, text) = await admin.OpsPostAsync($"{Path(role)}/delete", new { }).AnswerAsync();
        Assert.Equal((409, "OPS_ROLE_IN_USE"), (status, code));
        using (var held = JsonDocument.Parse(text))
        {
            Assert.Equal(2, held.RootElement.GetProperty("operators").GetInt32());
        }

        // Its holders are moved; then it goes.
        await world.Factory.OpsSqlAsync($"UPDATE [ops].[Operator] SET [RoleId] = '{TeamHelp.Viewer}' WHERE [RoleId] = '{role}'");
        Assert.Equal(400, (await admin.OpsPostAsync($"{Path(role)}/delete", new { note = new string('n', 501) }).AnswerAsync()).Status);
        Assert.Equal(204, (await admin.OpsPostAsync($"{Path(role)}/delete", new { note = "دُمج في دور آخر" }).AnswerAsync()).Status);

        Assert.DoesNotContain(role, (await admin.JsonAsync(TeamHelp.Roles)).GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()));
        Assert.DoesNotContain(role, (await admin.JsonAsync($"{TeamHelp.Roles}/options")).GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()));
        Assert.Equal(404, (await admin.OpsGetAsync(Path(role)).AnswerAsync()).Status);
        Assert.Equal(404, (await admin.OpsPostAsync($"{Path(role)}/delete", new { }).AnswerAsync()).Status);

        var entry = Assert.Single(await sql.PlatformEntriesAsync(role, "opsrole.deleted"));
        Assert.Equal(name, entry.TargetName);
        Assert.Contains("\"note\":\"دُمج في دور آخر\"", entry.Facts, StringComparison.Ordinal);

        // The name is free for a new role.
        Assert.Equal(201, (await admin.OpsPostAsync(TeamHelp.Roles, NewRole(name)).AnswerAsync()).Status);
        _ = holder;
    }
}
