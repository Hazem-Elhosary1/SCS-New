using System.Net;
using Scs.IntegrationTests.Fixtures;
using Scs.Operators.Domain;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, User Story 5 (FR-041 to FR-047): suspending, reactivating, sending a password link
/// and unlocking — each only in the state it belongs to, each recorded, none showing a link.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorAccountActionsTests(SqlServerFixture sql)
{
    private static string Path(Guid id, string action) => $"{TeamHelp.Operators}/{id}/{action}";

    private static Task<string> StateAsync(OpsWorld world, Guid id) =>
        world.Factory.OpsAsync(db => Task.FromResult(db.Operators.Where(o => o.Id == id).Select(o => o.State).Single()));

    [Fact]
    public async Task Only_who_manages_operators_does_any_of_the_four()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (target, _) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        var (_, viewerEmail) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        using var viewer = await world.Factory.SignInOperatorAsync(viewerEmail);
        using var nobody = world.Factory.Browser();

        foreach (var action in new[] { "suspend", "reactivate", "password-link", "unlock" })
        {
            var (status, code, _) = await viewer.OpsPostAsync(Path(target, action), new { }).AnswerAsync();
            Assert.Equal((403, "PERMISSION_REQUIRED"), (status, code));

            using var anonymous = await nobody.OpsPostAsync(Path(target, action), new { });
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        Assert.Equal(OperatorStates.Active, await StateAsync(world, target));
        Assert.Empty(await sql.PlatformEntriesAsync(target));
    }

    [Fact]
    public async Task Suspending_ends_the_sessions_and_the_account_is_told_only_after_the_right_password()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (target, email) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        using var theirs = await world.Factory.SignInOperatorAsync(email);

        var (tooLong, tooLongCode, _) = await admin.OpsPostAsync(Path(target, "suspend"), new { reason = new string('r', 501) }).AnswerAsync();
        Assert.Equal((400, "VALIDATION_FAILED"), (tooLong, tooLongCode));

        Assert.Equal(204, (await admin.OpsPostAsync(Path(target, "suspend"), new { reason = " غادر الفريق " }).AnswerAsync()).Status);

        Assert.Equal(OperatorStates.Suspended, await StateAsync(world, target));
        using var signedOut = await theirs.OpsGetAsync("/v1/ops/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        Assert.Equal(
            SessionEndings.Suspended,
            await world.Factory.OpsAsync(db => Task.FromResult(db.Sessions.Where(s => s.OperatorId == target).Select(s => s.EndedFor).Single())));

        var entry = Assert.Single(await sql.PlatformEntriesAsync(target, "operator.suspended"));
        Assert.Equal(world.OperatorId, entry.Actor);
        Assert.Contains("\"reason\":\"غادر الفريق\"", entry.Facts, StringComparison.Ordinal);

        // The right password is told the account is suspended; a wrong one gets the single answer.
        using var browser = world.Factory.Browser();
        var right = await browser.OpsStepOneAsync(email).AnswerAsync();
        var wrong = await browser.OpsStepOneAsync(email, "Wrong-Horse-9!").AnswerAsync();
        Assert.Equal("ACCOUNT_DISABLED", right.Code);
        Assert.Equal("INVALID_CREDENTIALS", wrong.Code);

        // Twice is refused: it is not active any more.
        var (again, againCode, _) = await admin.OpsPostAsync(Path(target, "suspend"), new { }).AnswerAsync();
        Assert.Equal((409, "OPS_STATE_CHANGED"), (again, againCode));
        Assert.Single(await sql.PlatformEntriesAsync(target, "operator.suspended"));
    }

    [Fact]
    public async Task Nobody_suspends_their_own_account_and_an_invited_one_is_not_suspended()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        var (own, ownCode, _) = await admin.OpsPostAsync(Path(world.OperatorId, "suspend"), new { }).AnswerAsync();
        Assert.Equal((403, "OPS_OWN_ACCOUNT"), (own, ownCode));
        Assert.Equal(OperatorStates.Active, await StateAsync(world, world.OperatorId));

        var (invited, _) = await world.Factory.CreateOperatorAsync(OperatorStates.Invited, roleId: TeamHelp.Viewer);
        var (status, code, _) = await admin.OpsPostAsync(Path(invited, "suspend"), new { }).AnswerAsync();
        Assert.Equal((409, "OPS_STATE_CHANGED"), (status, code));

        var (missing, missingCode, _) = await admin.OpsPostAsync(Path(Guid.NewGuid(), "suspend"), new { }).AnswerAsync();
        Assert.Equal((404, "NOT_FOUND"), (missing, missingCode));
    }

    [Fact]
    public async Task Reactivating_brings_a_suspended_account_back_with_its_tries_and_lock_forgotten()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (target, email) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);

        var (early, earlyCode, _) = await admin.OpsPostAsync(Path(target, "reactivate")).AnswerAsync();
        Assert.Equal((409, "OPS_STATE_CHANGED"), (early, earlyCode));

        Assert.Equal(204, (await admin.OpsPostAsync(Path(target, "suspend"), new { }).AnswerAsync()).Status);
        await world.Factory.OpsSqlAsync(
            $"UPDATE [ops].[Operator] SET [FailedAttempts] = 4, [SpentCodes] = 2, [LockedUntilUtc] = DATEADD(MINUTE, 10, SYSDATETIMEOFFSET()) WHERE [Id] = '{target}'");

        Assert.Equal(204, (await admin.OpsPostAsync(Path(target, "reactivate")).AnswerAsync()).Status);

        var after = await world.Factory.OpsAsync(db => Task.FromResult(
            db.Operators.Where(o => o.Id == target)
                .Select(o => new { o.State, o.FailedAttempts, o.SpentCodes, o.LockedUntilUtc, o.SuspendedAtUtc }).Single()));
        Assert.Equal(
            (OperatorStates.Active, (byte)0, (byte)0, (DateTimeOffset?)null, (DateTimeOffset?)null),
            (after.State, after.FailedAttempts, after.SpentCodes, after.LockedUntilUtc, after.SuspendedAtUtc));
        Assert.Single(await sql.PlatformEntriesAsync(target, "operator.reactivated"));

        using var back = await world.Factory.SignInOperatorAsync(email);
        using var session = await back.OpsGetAsync("/v1/ops/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [Fact]
    public async Task A_reset_link_goes_to_an_active_account_once_a_minute_and_is_shown_to_nobody()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (target, email) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);

        var (status, _, text) = await admin.OpsPostAsync(Path(target, "password-link")).AnswerAsync();
        Assert.Equal(202, status);
        Assert.Equal("", text);

        var message = world.Factory.Mail.To(email).Single();
        Assert.Contains("/password/reset#token=", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("30", message.TextBody, StringComparison.Ordinal);

        var entry = Assert.Single(await sql.PlatformEntriesAsync(target, "operator.link-sent"));
        Assert.Contains("\"kind\":\"reset\"", entry.Facts, StringComparison.Ordinal);
        Assert.DoesNotContain("token", entry.Facts + entry.Changes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", (await admin.JsonAsync($"{TeamHelp.Operators}/{target}")).GetRawText(), StringComparison.OrdinalIgnoreCase);

        // A second within the minute: nothing sent, nothing recorded.
        world.Factory.Clock.Advance(TimeSpan.FromSeconds(40));
        var (soon, soonCode, _) = await admin.OpsPostAsync(Path(target, "password-link")).AnswerAsync();
        Assert.Equal((409, "OPS_LINK_TOO_SOON"), (soon, soonCode));
        Assert.Single(world.Factory.Mail.To(email));
        Assert.Single(await sql.PlatformEntriesAsync(target, "operator.link-sent"));

        world.Factory.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(202, (await admin.OpsPostAsync(Path(target, "password-link")).AnswerAsync()).Status);
        Assert.Equal(2, world.Factory.Mail.To(email).Count);

        // The link works: the account's holder chooses a new password with it.
        using var browser = world.Factory.Browser();
        using var set = await browser.OpsPostAsync(
            "/v1/ops/password/set", new { token = world.Factory.LatestLinkToken(email), password = "Another-Horse-9!" });
        Assert.True(set.IsSuccessStatusCode);
    }

    [Fact]
    public async Task An_invited_account_gets_its_invitation_anew_and_a_suspended_one_gets_nothing()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var body = TeamHelp.NewOperator(TeamHelp.Viewer);
        var email = (string)body["email"]!;
        var (_, _, created) = await admin.OpsPostAsync(TeamHelp.Operators, body).AnswerAsync();
        var invited = System.Text.Json.JsonDocument.Parse(created).RootElement.GetProperty("id").GetGuid();

        // Just invited, the invitation still on its way: a link now would be a second within the minute.
        var (soon, soonCode, _) = await admin.OpsPostAsync(Path(invited, "password-link")).AnswerAsync();
        Assert.Equal((409, "OPS_LINK_TOO_SOON"), (soon, soonCode));

        await world.Factory.SendInvitationsAsync();
        var earlier = world.Factory.LatestLinkToken(email);
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(202, (await admin.OpsPostAsync(Path(invited, "password-link")).AnswerAsync()).Status);
        var anew = world.Factory.Mail.To(email)[^1];
        Assert.Contains("/password/first#token=", anew.TextBody, StringComparison.Ordinal);
        Assert.Contains("72", anew.TextBody, StringComparison.Ordinal);
        Assert.Contains("أضافك سارة أحمد بدور مشاهد فقط", anew.TextBody, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"invitation\"", Assert.Single(await sql.PlatformEntriesAsync(invited, "operator.link-sent")).Facts, StringComparison.Ordinal);

        // The earlier link stopped working when the new one was made.
        using var browser = world.Factory.Browser();
        using var old = await browser.OpsPostAsync("/v1/ops/password/link", new { token = earlier });
        Assert.NotEqual(HttpStatusCode.OK, old.StatusCode);
        using var fresh = await browser.OpsPostAsync("/v1/ops/password/link", new { token = world.Factory.LatestLinkToken(email) });
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);

        var (suspended, suspendedEmail) = await world.Factory.CreateOperatorAsync(OperatorStates.Suspended, roleId: TeamHelp.Viewer);
        var (status, code, _) = await admin.OpsPostAsync(Path(suspended, "password-link")).AnswerAsync();
        Assert.Equal((409, "OPS_STATE_CHANGED"), (status, code));
        Assert.Empty(world.Factory.Mail.To(suspendedEmail));
    }

    [Fact]
    public async Task A_link_that_cannot_leave_is_said_so_and_nothing_is_kept_of_it()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (target, email) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        world.Factory.Mail.FailingFor[email] = true;

        var (status, code, _) = await admin.OpsPostAsync(Path(target, "password-link")).AnswerAsync();

        Assert.Equal((503, "OPS_LINK_NOT_SENT"), (status, code));
        Assert.Equal(0, await world.Factory.OpsAsync(db => Task.FromResult(db.Links.Count(l => l.OperatorId == target))));
        Assert.Empty(await sql.PlatformEntriesAsync(target));

        // Nothing was kept, so trying again at once is not "too soon".
        world.Factory.Mail.FailingFor.Clear();
        Assert.Equal(202, (await admin.OpsPostAsync(Path(target, "password-link")).AnswerAsync()).Status);
    }

    [Fact]
    public async Task Unlocking_clears_a_lock_and_only_a_lock()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        var (target, email) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);

        var (early, earlyCode, _) = await admin.OpsPostAsync(Path(target, "unlock")).AnswerAsync();
        Assert.Equal((409, "OPS_STATE_CHANGED"), (early, earlyCode));

        await world.Factory.OpsSqlAsync(
            $"UPDATE [ops].[Operator] SET [FailedAttempts] = 5, [LockedUntilUtc] = DATEADD(MINUTE, 10, SYSDATETIMEOFFSET()) WHERE [Id] = '{target}'");
        using var browser = world.Factory.Browser();
        Assert.Equal("ACCOUNT_LOCKED", (await browser.OpsStepOneAsync(email).AnswerAsync()).Code);

        Assert.Equal(204, (await admin.OpsPostAsync(Path(target, "unlock")).AnswerAsync()).Status);
        Assert.Single(await sql.PlatformEntriesAsync(target, "operator.unlocked"));

        using var back = await world.Factory.SignInOperatorAsync(email);
        using var session = await back.OpsGetAsync("/v1/ops/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }
}
