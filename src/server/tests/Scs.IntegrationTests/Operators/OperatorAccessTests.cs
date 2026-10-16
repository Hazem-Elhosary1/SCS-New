using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, User Story 1 (FR-003, FR-005, FR-027a; SC-009): what an operator does to their own
/// account needs no permission; the session says what the role holds now; and the deployment
/// command gives «مسؤول عام».
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorAccessTests(SqlServerFixture sql)
{
    private static List<string> Codes(JsonElement session) =>
        [.. session.GetProperty("permissions").EnumerateArray().Select(code => code.GetString()!)];

    [Fact]
    public async Task What_an_operator_does_to_their_own_account_answers_for_any_role_and_for_no_clinic_session()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (_, email, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.ReportsExport);
        using var browser = await world.Factory.SignInOperatorAsync(email);

        using var session = await browser.OpsGetAsync("/v1/ops/auth/session");
        using var profile = await browser.OpsGetAsync("/v1/ops/profile");
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (session.StatusCode, profile.StatusCode));

        // The team is not theirs to see.
        var (status, code, text) = await browser.OpsGetAsync(TeamHelp.Operators).AnswerAsync();
        Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));
        Assert.Contains(OperatorPermissions.OperatorsView, text, StringComparison.Ordinal);

        var clinic = await sql.Main.SeedClinicAsync();
        var (_, staffEmail) = await world.Factory.CreateAccountAsync(clinic);
        using var staff = await world.Factory.SignInAsAsync(staffEmail, SignIn.Password);
        foreach (var path in new[] { "/v1/ops/auth/session", "/v1/ops/profile", TeamHelp.Operators, TeamHelp.Roles })
        {
            using var crossed = await staff.OpsGetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, crossed.StatusCode);
        }
    }

    [Fact]
    public async Task The_session_names_the_role_and_exactly_what_it_holds_now()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (id, email, roleId) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.TicketsReply);
        using var browser = await world.Factory.SignInOperatorAsync(email);

        var session = await browser.JsonAsync("/v1/ops/auth/session");
        Assert.Equal(["tickets.view", "tickets.reply"], Codes(session));
        var role = session.GetProperty("operator").GetProperty("role");
        Assert.Equal(roleId, role.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, role.GetProperty("systemKey").ValueKind);
        Assert.Equal(role.GetProperty("name").GetString(), session.GetProperty("operator").GetProperty("roleName").GetString());

        // The role changes: the same session's next read has the new set, and nobody was signed out.
        await world.Factory.SetRoleCodesAsync(roleId, OperatorPermissions.OperatorsManage);
        Assert.Equal(["operators.view", "operators.manage"], Codes(await browser.JsonAsync("/v1/ops/auth/session")));
        Assert.Equal(1, await world.Factory.OpsAsync(db => Task.FromResult(db.Sessions.Count(s => s.OperatorId == id && s.EndedAtUtc == null))));
    }

    [Fact]
    public async Task A_system_role_is_named_by_its_key_and_in_the_account_s_language()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        var session = await admin.JsonAsync("/v1/ops/auth/session");
        Assert.Equal("super-admin", session.GetProperty("operator").GetProperty("role").GetProperty("systemKey").GetString());
        Assert.Equal("مسؤول عام", session.GetProperty("operator").GetProperty("roleName").GetString());
        Assert.Equal(28, Codes(session).Count);

        var (_, english) = await world.Factory.CreateOperatorAsync(language: "en", roleId: TeamHelp.Viewer);
        using var viewer = await world.Factory.SignInOperatorAsync(english);
        var theirs = await viewer.JsonAsync("/v1/ops/auth/session");
        Assert.Equal("Viewer Only", theirs.GetProperty("operator").GetProperty("roleName").GetString());
        Assert.Equal(OperatorPermissions.SystemRoles.CodesOf(OperatorPermissions.SystemRoles.Viewer), Codes(theirs));

        var profile = await viewer.JsonAsync("/v1/ops/profile");
        Assert.Equal("viewer", profile.GetProperty("role").GetProperty("systemKey").GetString());
    }

    [Fact]
    public async Task The_deployment_command_gives_the_super_admin_role_and_records_the_invitation_as_the_platform_s()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var email = $"first-{Guid.NewGuid():N}@heta.example";

        var result = await world.Factory.InviteAsync("أول مشغل", email);
        var id = result.OperatorId!.Value;

        Assert.Equal(
            TeamHelp.SuperAdmin,
            await world.Factory.OpsAsync(db => Task.FromResult(db.Operators.Where(o => o.Id == id).Select(o => o.RoleId).Single())));
        Assert.Null(await world.Factory.OpsAsync(db => Task.FromResult(db.Operators.Where(o => o.Id == id).Select(o => o.InvitedById).Single())));

        var entry = Assert.Single(await sql.PlatformEntriesAsync(id, "operator.invited"));
        Assert.Equal(("system", (Guid?)null, "أول مشغل"), (entry.ActorKind, entry.Actor, entry.TargetName));
        Assert.Contains("\"by\":\"deployment\"", entry.Facts, StringComparison.Ordinal);
        Assert.Contains("\"systemRole\":\"super-admin\"", entry.Facts, StringComparison.Ordinal);
        Assert.DoesNotContain(email, entry.Facts + entry.Changes, StringComparison.OrdinalIgnoreCase);

        // The invitation still names nobody as the inviter: the command's own words.
        Assert.True(await world.Factory.SendInvitationsAsync() >= 1);
        var message = world.Factory.Mail.To(email).Single();
        Assert.Contains("أُضفت إلى لوحة التشغيل", message.TextBody, StringComparison.Ordinal);
    }
}
