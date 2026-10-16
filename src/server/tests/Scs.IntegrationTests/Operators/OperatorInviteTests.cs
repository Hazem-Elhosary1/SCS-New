using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.Operators.Domain;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, User Story 2 (FR-027 to FR-033, FR-043): an operator invites a colleague, who joins
/// with nothing but their mailbox. No screen and no answer ever holds the link.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorInviteTests(SqlServerFixture sql)
{
    private static Guid IdOf(string text) => JsonDocument.Parse(text).RootElement.GetProperty("id").GetGuid();

    [Fact]
    public async Task Only_who_manages_operators_invites_or_reads_the_role_picker()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (_, viewerEmail) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        using var viewer = await world.Factory.SignInOperatorAsync(viewerEmail);

        var (status, code, text) = await viewer.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(TeamHelp.Viewer)).AnswerAsync();
        Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));
        Assert.Contains(OperatorPermissions.OperatorsManage, text, StringComparison.Ordinal);

        var (options, optionsCode, _) = await viewer.OpsGetAsync($"{TeamHelp.Roles}/options").AnswerAsync();
        Assert.Equal((403, "PERMISSION_REQUIRED"), (options, optionsCode));

        using var nobody = world.Factory.Browser();
        using var anonymous = await nobody.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(TeamHelp.Viewer));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task An_invited_operator_gets_an_email_naming_the_inviter_and_the_role_and_joins_through_it()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var email = $"Nour.{Guid.NewGuid():N}@Heta.Example";

        var body = TeamHelp.NewOperator(TeamHelp.BillingAdmin, email)
            .With("mobile", " +20 100 111 2233 ").With("department", "billing").With("jobTitle", "محاسبة").With("language", "en");
        var (status, _, text) = await admin.OpsPostAsync(TeamHelp.Operators, body).AnswerAsync();
        Assert.Equal(201, status);
        var id = IdOf(text);
        Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);

        var detail = await admin.JsonAsync($"{TeamHelp.Operators}/{id}");
        Assert.Equal(
            ("نور خالد", email.ToLowerInvariant(), "invited", "+20 100 111 2233", "billing", "محاسبة", "en", "billing-admin"),
            (detail.GetProperty("fullName").GetString(), detail.GetProperty("email").GetString(), detail.GetProperty("state").GetString(),
                detail.GetProperty("mobile").GetString(), detail.GetProperty("department").GetString(),
                detail.GetProperty("jobTitle").GetString(), detail.GetProperty("language").GetString(),
                detail.GetProperty("role").GetProperty("systemKey").GetString()));
        Assert.Equal(world.OperatorId, detail.GetProperty("invitedBy").GetProperty("id").GetGuid());
        Assert.DoesNotContain("token", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);

        // No password yet, and nothing sent until the platform's own job runs.
        Assert.Empty(world.Factory.Mail.To(email));
        Assert.Null(await world.Factory.OpsAsync(db => Task.FromResult(db.Operators.Where(o => o.Id == id).Select(o => o.PasswordHash).Single())));

        Assert.True(await world.Factory.SendInvitationsAsync() >= 1);
        var message = world.Factory.Mail.To(email).Single();
        Assert.Contains("سارة أحمد added you as a Billing Manager", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("72 hours", message.TextBody, StringComparison.Ordinal);

        // Recorded with the role and without the link or the address.
        var entry = Assert.Single(await sql.PlatformEntriesAsync(id, "operator.invited"));
        Assert.Equal(("operator", (Guid?)world.OperatorId, "نور خالد"), (entry.ActorKind, entry.Actor, entry.TargetName));
        Assert.Contains("\"systemRole\":\"billing-admin\"", entry.Facts, StringComparison.Ordinal);
        Assert.Contains("\"sent\":true", entry.Facts, StringComparison.Ordinal);
        Assert.DoesNotContain("token", entry.Facts + entry.Changes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("heta.example", entry.Facts + entry.Changes, StringComparison.OrdinalIgnoreCase);

        // The mailbox is all the new operator needs.
        using var browser = world.Factory.Browser();
        var token = world.Factory.LatestLinkToken(email);
        using var set = await browser.OpsPostAsync("/v1/ops/password/set", new { token, password = OpsWorld.Password });
        Assert.True(set.IsSuccessStatusCode, $"Setting the first password answered {(int)set.StatusCode}.");
        using var joined = await world.Factory.SignInOperatorAsync(email.ToLowerInvariant());
        var session = await joined.JsonAsync("/v1/ops/auth/session");
        Assert.Equal("Billing Manager", session.GetProperty("operator").GetProperty("roleName").GetString());
    }

    [Fact]
    public async Task Without_send_now_the_account_is_made_and_nothing_is_owed_or_sent()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var role = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsReply);
        var body = TeamHelp.NewOperator(role).With("sendInvitation", false).With("twoStep", false);

        var (status, _, text) = await admin.OpsPostAsync(TeamHelp.Operators, body).AnswerAsync();
        Assert.Equal(201, status);
        var id = IdOf(text);

        await world.Factory.SendInvitationsAsync();
        Assert.Empty(world.Factory.Mail.To((string)body["email"]!));

        // The code by email is asked of everyone: the box cannot turn it off.
        Assert.True((await admin.JsonAsync($"{TeamHelp.Operators}/{id}")).GetProperty("twoStep").GetBoolean());

        var entry = Assert.Single(await sql.PlatformEntriesAsync(id, "operator.invited"));
        Assert.Contains("\"sent\":false", entry.Facts, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"Role ", entry.Facts, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fullName", "", "REQUIRED")]
    [InlineData("fullName", "201", "TOO_LONG")]
    [InlineData("email", "", "REQUIRED")]
    [InlineData("email", "not-an-address", "INVALID")]
    [InlineData("roleId", null, "REQUIRED")]
    [InlineData("mobile", "31", "TOO_LONG")]
    [InlineData("department", "marketing", "INVALID")]
    [InlineData("jobTitle", "151", "TOO_LONG")]
    [InlineData("language", "fr", "INVALID")]
    [InlineData("language", null, "REQUIRED")]
    public async Task Each_limit_of_the_form_is_refused_naming_its_field(string field, string? value, string expected)
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        // A number alone stands for "that many characters".
        object? sent = int.TryParse(value, out var length) ? new string('n', length) : value;
        var (status, code, text) = await admin.OpsPostAsync(
            TeamHelp.Operators, TeamHelp.NewOperator(TeamHelp.Viewer).With(field, sent)).AnswerAsync();

        Assert.Equal((400, "VALIDATION_FAILED"), (status, code));
        using var body = JsonDocument.Parse(text);
        Assert.Equal(expected, body.RootElement.GetProperty("errors").GetProperty(field)[0].GetString());
    }

    [Fact]
    public async Task The_longest_values_are_taken_and_an_email_is_one_operator_s()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        var longest = TeamHelp.NewOperator(TeamHelp.Viewer)
            .With("fullName", new string('n', 200)).With("mobile", new string('1', 30)).With("jobTitle", new string('t', 150));
        Assert.Equal(201, (await admin.OpsPostAsync(TeamHelp.Operators, longest).AnswerAsync()).Status);

        // The same address in another case is the same address.
        var again = TeamHelp.NewOperator(TeamHelp.Viewer, ((string)longest["email"]!).ToUpperInvariant());
        var (status, code, _) = await admin.OpsPostAsync(TeamHelp.Operators, again).AnswerAsync();
        Assert.Equal((409, "OPS_EMAIL_TAKEN"), (status, code));

        // A role that does not exist, or was deleted, is not a role.
        var (unknown, unknownCode, _) = await admin.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(Guid.NewGuid())).AnswerAsync();
        Assert.Equal((400, "VALIDATION_FAILED"), (unknown, unknownCode));

        var deleted = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsView);
        Assert.Equal(204, (await admin.OpsPostAsync($"{TeamHelp.Roles}/{deleted}/delete", new { }).AnswerAsync()).Status);
        var (gone, goneCode, _) = await admin.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(deleted)).AnswerAsync();
        Assert.Equal((400, "VALIDATION_FAILED"), (gone, goneCode));
    }

    [Fact]
    public async Task Nobody_grants_a_role_that_holds_more_than_they_do_and_the_picker_says_which()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (_, managerEmail, ownRole) = await world.Factory.CreateOperatorWithAsync(
            OperatorPermissions.OperatorsManage, OperatorPermissions.TicketsReply);
        using var manager = await world.Factory.SignInOperatorAsync(managerEmail);

        var within = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsReply);
        var beyond = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsReply, OperatorPermissions.SettingsEdit);

        var options = (await manager.JsonAsync($"{TeamHelp.Roles}/options")).GetProperty("items").EnumerateArray()
            .ToDictionary(o => o.GetProperty("id").GetGuid(), o => (
                Grantable: o.GetProperty("grantable").GetBoolean(),
                Reason: o.GetProperty("reason").GetString()));

        Assert.Equal((true, null), options[within]);
        Assert.Equal((true, null), options[ownRole]);
        Assert.Equal((false, "holds-more"), options[beyond]);
        Assert.Equal((false, "super-admin-only"), options[TeamHelp.SuperAdmin]);
        Assert.Equal((false, "holds-more"), options[TeamHelp.Viewer]);

        Assert.Equal(201, (await manager.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(within)).AnswerAsync()).Status);

        foreach (var refused in new[] { beyond, TeamHelp.SuperAdmin, TeamHelp.Viewer })
        {
            var body = TeamHelp.NewOperator(refused);
            var (status, code, _) = await manager.OpsPostAsync(TeamHelp.Operators, body).AnswerAsync();
            Assert.Equal((403, "OPS_ROLE_NOT_GRANTABLE"), (status, code));
            Assert.Empty(await sql.OpsColumnAsync($"SELECT Email FROM [ops].[Operator] WHERE Email = '{body["email"]}'"));
        }

        // A super admin gives any role, the super admin's among them.
        using var admin = await world.SignInAsync();
        var theirs = (await admin.JsonAsync($"{TeamHelp.Roles}/options")).GetProperty("items").EnumerateArray().ToList();
        Assert.All(theirs, o => Assert.True(o.GetProperty("grantable").GetBoolean()));
        Assert.Equal(
            ["super-admin", "billing-admin", "viewer"],
            theirs.Take(3).Select(o => o.GetProperty("systemKey").GetString()));
        Assert.Equal(201, (await admin.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(TeamHelp.SuperAdmin)).AnswerAsync()).Status);
    }

    [Fact]
    public async Task An_invitation_lives_seventy_two_hours_and_a_reset_link_thirty_minutes()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var body = TeamHelp.NewOperator(TeamHelp.Viewer);
        var email = (string)body["email"]!;
        Assert.Equal(201, (await admin.OpsPostAsync(TeamHelp.Operators, body).AnswerAsync()).Status);
        await world.Factory.SendInvitationsAsync();
        var invitation = world.Factory.LatestLinkToken(email);

        using var browser = world.Factory.Browser();
        world.Factory.Clock.Advance(TimeSpan.FromHours(71));
        using var stillGood = await browser.OpsPostAsync("/v1/ops/password/link", new { token = invitation });
        Assert.Equal(HttpStatusCode.OK, stillGood.StatusCode);

        world.Factory.Clock.Advance(TimeSpan.FromHours(1.1));
        var (ended, endedCode, endedText) = await browser.OpsPostAsync("/v1/ops/password/link", new { token = invitation }).AnswerAsync();
        Assert.NotEqual(200, ended);
        Assert.False(string.IsNullOrEmpty(endedCode));

        // What an ended invitation says is its own: the person is sent back to whoever invited them.
        Assert.Contains("first", endedText, StringComparison.OrdinalIgnoreCase);

        // A reset link, for the inviter's own account, is good for thirty minutes.
        using var forgot = await browser.OpsPostAsync("/v1/ops/password/forgot", new { email = world.Email });
        var reset = world.Factory.LatestLinkToken(world.Email);
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(29));
        using var fresh = await browser.OpsPostAsync("/v1/ops/password/link", new { token = reset });
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        var (late, _, lateText) = await browser.OpsPostAsync("/v1/ops/password/link", new { token = reset }).AnswerAsync();
        Assert.NotEqual(200, late);
        Assert.Contains("reset", lateText, StringComparison.OrdinalIgnoreCase);
    }
}
