using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.Operators.Domain;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, User Story 3 (FR-025, FR-026): one operator's page — who they are, what their role
/// holds, what they did lately, and what the person looking may do to the account.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorDetailTests(SqlServerFixture sql)
{
    private static (bool Edit, bool Suspend, bool Reactivate, bool SendLink, bool Unlock) Can(JsonElement detail)
    {
        var can = detail.GetProperty("can");
        return (
            can.GetProperty("edit").GetBoolean(), can.GetProperty("suspend").GetBoolean(), can.GetProperty("reactivate").GetBoolean(),
            can.GetProperty("sendLink").GetBoolean(), can.GetProperty("unlock").GetBoolean());
    }

    [Fact]
    public async Task Only_who_may_see_the_team_reads_an_operator_and_an_unknown_one_is_not_found()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        var (_, without, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.TicketsView);
        using var other = await world.Factory.SignInOperatorAsync(without);
        var (status, code, _) = await other.OpsGetAsync($"{TeamHelp.Operators}/{world.OperatorId}").AnswerAsync();
        Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));

        // The same refusal for an account that does not exist: nothing is learnt from it.
        var (missing, missingCode, _) = await other.OpsGetAsync($"{TeamHelp.Operators}/{Guid.NewGuid()}").AnswerAsync();
        Assert.Equal((403, "PERMISSION_REQUIRED"), (missing, missingCode));

        var (notFound, notFoundCode, _) = await admin.OpsGetAsync($"{TeamHelp.Operators}/{Guid.NewGuid()}").AnswerAsync();
        Assert.Equal((404, "NOT_FOUND"), (notFound, notFoundCode));
    }

    [Fact]
    public async Task The_page_holds_the_account_its_role_s_permissions_and_no_secret()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (id, email, roleId) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.TicketsReply, OperatorPermissions.BillingView);
        await world.Factory.OpsSqlAsync(
            $"UPDATE [ops].[Operator] SET [Mobile] = '+20 100 111 2233', [Department] = 'support', [JobTitle] = N'أخصائي دعم فني' WHERE [Id] = '{id}'");

        var detail = await admin.JsonAsync($"{TeamHelp.Operators}/{id}");

        Assert.Equal(
            (id, email, "active", true, "+20 100 111 2233", "support", "أخصائي دعم فني", "ar"),
            (detail.GetProperty("id").GetGuid(), detail.GetProperty("email").GetString(), detail.GetProperty("state").GetString(),
                detail.GetProperty("twoStep").GetBoolean(), detail.GetProperty("mobile").GetString(),
                detail.GetProperty("department").GetString(), detail.GetProperty("jobTitle").GetString(),
                detail.GetProperty("language").GetString()));
        Assert.Equal(roleId, detail.GetProperty("role").GetProperty("id").GetGuid());
        Assert.Equal(
            ["tickets.view", "tickets.reply", "billing.view"],
            detail.GetProperty("permissions").EnumerateArray().Select(c => c.GetString()));
        Assert.False(detail.GetProperty("locked").GetBoolean());
        Assert.False(string.IsNullOrEmpty(detail.GetProperty("version").GetString()));
        Assert.True(detail.GetProperty("createdAtUtc").GetDateTimeOffset() <= world.Factory.Clock.GetUtcNow());

        var performance = Assert.Single(detail.GetProperty("performance").EnumerateArray());
        Assert.Equal(("activeSessions", 0), (performance.GetProperty("key").GetString(), performance.GetProperty("value").GetInt32()));

        var text = detail.GetRawText();
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lockedUntil", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("failed", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Who_added_the_account_is_the_inviter_or_the_system_and_the_first_operator_is_marked()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        // Made by the deployment tool: no inviter. The earliest such account is the first operator.
        var theirs = await admin.JsonAsync($"{TeamHelp.Operators}/{world.OperatorId}");
        Assert.Equal(JsonValueKind.Null, theirs.GetProperty("invitedBy").ValueKind);

        var earliest = Guid.Parse((await sql.OpsColumnAsync(
            "SELECT TOP 1 CAST(Id AS varchar(36)) FROM [ops].[Operator] WHERE InvitedById IS NULL ORDER BY CreatedAtUtc")).Single());
        Assert.True((await admin.JsonAsync($"{TeamHelp.Operators}/{earliest}")).GetProperty("firstOperator").GetBoolean());
        if (earliest != world.OperatorId)
        {
            Assert.False(theirs.GetProperty("firstOperator").GetBoolean());
        }

        // Invited from the console: the inviter, by the name they had.
        var (status, _, text) = await admin.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(TeamHelp.Viewer)).AnswerAsync();
        Assert.Equal(201, status);
        var invited = JsonDocument.Parse(text).RootElement.GetProperty("id").GetGuid();

        var detail = await admin.JsonAsync($"{TeamHelp.Operators}/{invited}");
        Assert.Equal(world.OperatorId, detail.GetProperty("invitedBy").GetProperty("id").GetGuid());
        Assert.Equal("سارة أحمد", detail.GetProperty("invitedBy").GetProperty("name").GetString());
        Assert.False(detail.GetProperty("firstOperator").GetBoolean());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("lastSignInAtUtc").ValueKind);
    }

    [Fact]
    public async Task Recent_activity_is_the_newest_ten_of_sign_ins_and_of_what_the_operator_did()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        var first = TeamHelp.NewOperator(TeamHelp.Viewer);
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(201, (await admin.OpsPostAsync(TeamHelp.Operators, first).AnswerAsync()).Status);
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(2));
        var role = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsView);
        Assert.Equal(201, (await admin.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(role).With("fullName", "Omar Adel")).AnswerAsync()).Status);

        var detail = await admin.JsonAsync($"{TeamHelp.Operators}/{world.OperatorId}");
        var activity = detail.GetProperty("activity").EnumerateArray().ToList();

        Assert.Equal(["operator.invited", "operator.invited", "signed-in"], activity.Select(a => a.GetProperty("kind").GetString()));
        Assert.Equal("Omar Adel", activity[0].GetProperty("name").GetString());
        Assert.False(string.IsNullOrEmpty(activity[0].GetProperty("role").GetString()));
        Assert.Equal("viewer", activity[1].GetProperty("roleKey").GetString());
        Assert.False(string.IsNullOrEmpty(activity[2].GetProperty("device").GetString()));
        Assert.True(activity[0].GetProperty("atUtc").GetDateTimeOffset() > activity[1].GetProperty("atUtc").GetDateTimeOffset());

        // One session is open: their own.
        Assert.Equal(1, detail.GetProperty("performance")[0].GetProperty("value").GetInt32());

        // Never more than ten.
        for (var i = 0; i < 11; i++)
        {
            world.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(201, (await admin.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(TeamHelp.Viewer)).AnswerAsync()).Status);
        }

        var later = await admin.JsonAsync($"{TeamHelp.Operators}/{world.OperatorId}");
        Assert.Equal(10, later.GetProperty("activity").GetArrayLength());
        Assert.All(later.GetProperty("activity").EnumerateArray(), a => Assert.Equal("operator.invited", a.GetProperty("kind").GetString()));
    }

    [Fact]
    public async Task What_may_be_done_to_an_account_follows_its_state_and_who_is_looking()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        var (active, _) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        var (invited, _) = await world.Factory.CreateOperatorAsync(OperatorStates.Invited, roleId: TeamHelp.Viewer);
        var (suspended, _) = await world.Factory.CreateOperatorAsync(OperatorStates.Suspended, roleId: TeamHelp.Viewer);

        Assert.Equal((true, true, false, true, false), Can(await admin.JsonAsync($"{TeamHelp.Operators}/{active}")));
        Assert.Equal((true, false, false, true, false), Can(await admin.JsonAsync($"{TeamHelp.Operators}/{invited}")));
        Assert.Equal((true, false, true, false, false), Can(await admin.JsonAsync($"{TeamHelp.Operators}/{suspended}")));

        // Nobody suspends their own account.
        Assert.Equal((true, false, false, true, false), Can(await admin.JsonAsync($"{TeamHelp.Operators}/{world.OperatorId}")));

        // Unlocking is offered only while the account is locked.
        await world.Factory.OpsSqlAsync(
            $"UPDATE [ops].[Operator] SET [LockedUntilUtc] = DATEADD(MINUTE, 10, SYSDATETIMEOFFSET()) WHERE [Id] = '{active}'");
        var locked = await admin.JsonAsync($"{TeamHelp.Operators}/{active}");
        Assert.True(locked.GetProperty("locked").GetBoolean());
        Assert.Equal((true, true, false, true, true), Can(locked));

        // Whoever only sees the team does nothing to it.
        var (_, viewerEmail) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        using var viewer = await world.Factory.SignInOperatorAsync(viewerEmail);
        Assert.Equal((false, false, false, false, false), Can(await viewer.JsonAsync($"{TeamHelp.Operators}/{active}")));
        using var refused = await viewer.OpsPostAsync($"{TeamHelp.Operators}/{active}/unlock");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }
}
