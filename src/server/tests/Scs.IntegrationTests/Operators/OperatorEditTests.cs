using System.Net;
using Scs.IntegrationTests.Fixtures;
using Scs.Operators.Domain;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, User Story 4 (FR-034 to FR-039): an operator's details and role are changed within
/// the guards — never the email, never one's own role, never a role beyond the editor's own,
/// never the last «مسؤول عام» — and a changed role signs the person out.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorEditTests(SqlServerFixture sql)
{
    private static string Path(Guid id) => $"{TeamHelp.Operators}/{id}";

    private static Task<int> OpenSessionsAsync(OpsWorld world, Guid id) =>
        world.Factory.OpsAsync(db => Task.FromResult(db.Sessions.Count(s => s.OperatorId == id && s.EndedAtUtc == null)));

    [Fact]
    public async Task Only_who_manages_operators_edits_one()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (target, _) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        var body = await admin.EditBodyAsync(target);

        var (_, viewerEmail) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        using var viewer = await world.Factory.SignInOperatorAsync(viewerEmail);
        var (status, code, _) = await viewer.OpsPutAsync(Path(target), body).AnswerAsync();
        Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));

        var (missing, missingCode, _) = await admin.OpsPutAsync(Path(Guid.NewGuid()), body).AnswerAsync();
        Assert.Equal((404, "NOT_FOUND"), (missing, missingCode));
    }

    [Fact]
    public async Task Every_field_but_the_email_changes_and_is_recorded_before_and_after_and_no_session_ends()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (target, email) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        using var theirs = await world.Factory.SignInOperatorAsync(email);

        var body = (await admin.EditBodyAsync(target))
            .With("fullName", "  سارة علي ").With("mobile", "+20 111 000 2222").With("department", "sales")
            .With("jobTitle", "مديرة مبيعات").With("language", "en").With("note", " انتقلت إلى المبيعات ")
            .With("email", "someone-else@heta.example");
        Assert.Equal(204, (await admin.OpsPutAsync(Path(target), body).AnswerAsync()).Status);

        var after = await admin.JsonAsync(Path(target));
        Assert.Equal(
            ("سارة علي", email, "+20 111 000 2222", "sales", "مديرة مبيعات", "en"),
            (after.GetProperty("fullName").GetString(), after.GetProperty("email").GetString(), after.GetProperty("mobile").GetString(),
                after.GetProperty("department").GetString(), after.GetProperty("jobTitle").GetString(),
                after.GetProperty("language").GetString()));
        Assert.NotEqual(body["version"], after.GetProperty("version").GetString());

        // A change of name or department signs nobody out.
        Assert.Equal(1, await OpenSessionsAsync(world, target));
        using var still = await theirs.OpsGetAsync("/v1/ops/auth/session");
        Assert.Equal(HttpStatusCode.OK, still.StatusCode);

        var entry = Assert.Single(await sql.PlatformEntriesAsync(target, "operator.edited"));
        Assert.Equal(world.OperatorId, entry.Actor);
        Assert.Contains("\"note\":\"انتقلت إلى المبيعات\"", entry.Facts, StringComparison.Ordinal);
        Assert.True(entry.Changes.Contains("{\"field\":\"fullName\",\"before\":\"سارة أحمد\",\"after\":\"سارة علي\",", StringComparison.Ordinal), entry.Changes);
        Assert.Contains("{\"field\":\"department\",\"after\":\"sales\",", entry.Changes, StringComparison.Ordinal);
        Assert.Contains("{\"field\":\"language\",\"before\":\"ar\",\"after\":\"en\",", entry.Changes, StringComparison.Ordinal);
        Assert.Empty(await sql.PlatformEntriesAsync(target, "operator.role-changed"));

        // Saving the same again changes nothing and records nothing.
        Assert.Equal(204, (await admin.OpsPutAsync(Path(target), await admin.EditBodyAsync(target)).AnswerAsync()).Status);
        Assert.Single(await sql.PlatformEntriesAsync(target, "operator.edited"));
    }

    [Fact]
    public async Task The_limits_hold_and_a_stale_version_is_refused()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (target, _) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        var body = await admin.EditBodyAsync(target);

        foreach (var (field, value) in new (string, object?)[]
                 {
                     ("fullName", " "), ("fullName", new string('n', 201)), ("mobile", new string('1', 31)),
                     ("department", "marketing"), ("jobTitle", new string('t', 151)), ("language", "fr"),
                     ("note", new string('n', 501)), ("version", null), ("roleId", null),
                 })
        {
            var (status, code, text) = await admin.OpsPutAsync(Path(target), body.With(field, value)).AnswerAsync();
            Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
            Assert.Contains($"\"{field}\"", text, StringComparison.Ordinal);
        }

        Assert.Equal(204, (await admin.OpsPutAsync(Path(target), body.With("note", new string('n', 500)).With("jobTitle", "أول")).AnswerAsync()).Status);

        // The form still holds the version it was opened with.
        var (stale, staleCode, _) = await admin.OpsPutAsync(Path(target), body.With("jobTitle", "ثانٍ")).AnswerAsync();
        Assert.Equal((409, "OPS_CHANGED_ELSEWHERE"), (stale, staleCode));
        Assert.Equal("أول", (await admin.JsonAsync(Path(target))).GetProperty("jobTitle").GetString());
    }

    [Fact]
    public async Task A_changed_role_is_recorded_and_ends_every_session_of_the_account()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var custom = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsReply);
        var (target, email) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        using var theirs = await world.Factory.SignInOperatorAsync(email);

        var body = (await admin.EditBodyAsync(target)).With("roleId", custom).With("note", "إلى الدعم");
        Assert.Equal(204, (await admin.OpsPutAsync(Path(target), body).AnswerAsync()).Status);

        Assert.Equal(custom, (await admin.JsonAsync(Path(target))).GetProperty("role").GetProperty("id").GetGuid());
        Assert.Equal(0, await OpenSessionsAsync(world, target));
        using var out1 = await theirs.OpsGetAsync("/v1/ops/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, out1.StatusCode);
        Assert.Equal(
            SessionEndings.RoleChanged,
            await world.Factory.OpsAsync(db => Task.FromResult(db.Sessions.Where(s => s.OperatorId == target).Select(s => s.EndedFor).Single())));

        var entry = Assert.Single(await sql.PlatformEntriesAsync(target, "operator.role-changed"));
        Assert.Contains("\"beforeKey\":\"viewer\"", entry.Changes, StringComparison.Ordinal);
        Assert.Contains("\"after\":\"Role ", entry.Changes, StringComparison.Ordinal);
        Assert.Contains("\"note\":\"إلى الدعم\"", entry.Facts, StringComparison.Ordinal);
        Assert.Empty(await sql.PlatformEntriesAsync(target, "operator.edited"));

        // The editor's own page tells of it.
        var activity = (await admin.JsonAsync(Path(world.OperatorId))).GetProperty("activity")[0];
        Assert.Equal("operator.role-changed", activity.GetProperty("kind").GetString());
        Assert.StartsWith("Role ", activity.GetProperty("role").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nobody_changes_their_own_role_but_their_other_details_save()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var body = await admin.EditBodyAsync(world.OperatorId);

        var (status, code, _) = await admin.OpsPutAsync(Path(world.OperatorId), body.With("roleId", TeamHelp.Viewer)).AnswerAsync();
        Assert.Equal((403, "OPS_OWN_ROLE"), (status, code));

        Assert.Equal(204, (await admin.OpsPutAsync(Path(world.OperatorId), body.With("jobTitle", "مديرة المنصة")).AnswerAsync()).Status);
        var after = await admin.JsonAsync(Path(world.OperatorId));
        Assert.Equal(("مديرة المنصة", "super-admin"), (after.GetProperty("jobTitle").GetString(), after.GetProperty("role").GetProperty("systemKey").GetString()));
        Assert.Equal(1, await OpenSessionsAsync(world, world.OperatorId));
    }

    [Fact]
    public async Task Nobody_gives_a_role_beyond_their_own()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (_, managerEmail, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.OperatorsManage, OperatorPermissions.TicketsReply);
        using var manager = await world.Factory.SignInOperatorAsync(managerEmail);
        var within = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsView);
        var beyond = await world.Factory.CreateRoleAsync(OperatorPermissions.SettingsEdit);
        var (target, _) = await world.Factory.CreateOperatorAsync(roleId: within);
        var body = await manager.EditBodyAsync(target);

        foreach (var role in new[] { beyond, TeamHelp.SuperAdmin, TeamHelp.BillingAdmin })
        {
            var (status, code, _) = await manager.OpsPutAsync(Path(target), body.With("roleId", role)).AnswerAsync();
            Assert.Equal((403, "OPS_ROLE_NOT_GRANTABLE"), (status, code));
        }

        var (unknown, unknownCode, _) = await manager.OpsPutAsync(Path(target), body.With("roleId", Guid.NewGuid())).AnswerAsync();
        Assert.Equal((400, "VALIDATION_FAILED"), (unknown, unknownCode));

        Assert.Equal(within, (await manager.JsonAsync(Path(target))).GetProperty("role").GetProperty("id").GetGuid());
        Assert.Empty(await sql.PlatformEntriesAsync(target));
    }
}
