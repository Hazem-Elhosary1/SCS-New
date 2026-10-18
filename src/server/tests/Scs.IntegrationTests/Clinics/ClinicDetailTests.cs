using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.ClinicFixtures;

namespace Scs.IntegrationTests.Clinics;

/// <summary>
/// Spec 015, User Stories 3 and 5 (FR-014 – FR-019, FR-023, FR-030): a clinic's detail, its account
/// manager, the owner's invitation and its resend, the settings tab, and the operator page's
/// «عيادات تم إدارتها».
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClinicDetailTests(SqlServerFixture sql)
{
    /// <summary>The operator who added each world's clinic.</summary>
    private static readonly Dictionary<OpsWorld, Guid> Editors = new(ReferenceEqualityComparer.Instance);

    private static async Task<(OpsWorld World, HttpClient Editor, Guid Id, string Email)> AddedAsync(SqlServerFixture sql)
    {
        var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        var (me, editor) = await world.Factory.SignInEditorAsync();
        Editors[world] = me;
        var email = NewEmail();
        var (id, _) = await editor.AddClinicAsync(NewClinic(plan, email: email));
        return (world, editor, id, email);
    }

    [Fact]
    public async Task The_detail_shows_every_row_of_the_card()
    {
        var (world, editor, id, email) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var detail = await editor.JsonAsync($"{ClinicsPath}/{id}");

            Assert.Equal("trial", detail.GetProperty("status").GetProperty("code").GetString());
            Assert.Equal("https://app.scs-heta.com", detail.GetProperty("signInAddress").GetString());
            Assert.Equal("14 شارع التحرير، القاهرة", detail.GetProperty("address").GetString());
            Assert.Equal("123-456-789", detail.GetProperty("taxNumber").GetString());
            Assert.Equal("dental", detail.GetProperty("specialty").GetString());
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("billingCycle").ValueKind);
            Assert.Equal(JsonValueKind.String, detail.GetProperty("nextRenewal").ValueKind);
            Assert.Equal((0, 1), (detail.GetProperty("doctors").GetInt32(), detail.GetProperty("branches").GetInt32()));
            Assert.Equal(Editors[world], detail.GetProperty("createdBy").GetProperty("id").GetGuid());

            var owner = detail.GetProperty("owner");
            Assert.Equal(email, owner.GetProperty("email").GetString());
            Assert.Equal("+201001234567", owner.GetProperty("mobile").GetString());
            Assert.Equal("invited", owner.GetProperty("state").GetString());
            Assert.Equal(JsonValueKind.Null, owner.GetProperty("lastSignInAt").ValueKind);
            Assert.Equal("live", owner.GetProperty("invitation").GetProperty("state").GetString());
            Assert.True(detail.GetProperty("can").GetProperty("edit").GetBoolean());
            Assert.Contains(detail.GetProperty("managers").EnumerateArray(), m => m.GetProperty("id").GetGuid() == Editors[world]);
        }
    }

    [Fact]
    public async Task A_lapsed_invitation_reads_lapsed_and_a_viewer_reads_no_actions()
    {
        var (world, editor, id, _) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            // The viewer is made before the clock moves, with the system role: a role or an operator
            // dated 73 hours ahead would sort after every later test's. It signs in afterwards.
            var (_, viewerEmail) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
            world.Factory.Clock.Advance(TimeSpan.FromHours(73));
            using var viewer = await world.Factory.SignInOperatorAsync(viewerEmail);
            var detail = await viewer.JsonAsync($"{ClinicsPath}/{id}");

            Assert.Equal("lapsed", detail.GetProperty("owner").GetProperty("invitation").GetProperty("state").GetString());
            Assert.False(detail.GetProperty("can").GetProperty("edit").GetBoolean());
            Assert.Empty(detail.GetProperty("managers").EnumerateArray());
        }
    }

    [Fact]
    public async Task A_seeded_clinic_reads_with_what_it_lacks_empty()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var viewer = await world.Factory.SignInWithAsync(Viewer);
        var id = await sql.Main.SeedClinicAsync();

        var detail = await viewer.JsonAsync($"{ClinicsPath}/{id}");

        Assert.Equal(JsonValueKind.Null, detail.GetProperty("address").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("plan").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("owner").ValueKind);
        Assert.Equal("active", detail.GetProperty("status").GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_account_manager_is_assigned_cleared_and_refused_when_not_active()
    {
        var (world, editor, id, _) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var (manager, _, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.ClinicsView);
            var version = (await editor.JsonAsync($"{ClinicsPath}/{id}")).GetProperty("version").GetString();

            var saved = await editor.OpsPutAsync($"{ClinicsPath}/{id}/account-manager", new { version, operatorId = manager }).AnswerAsync();
            Assert.Equal(200, saved.Status);
            var after = JsonDocument.Parse(saved.Text).RootElement;
            Assert.Equal(manager, after.GetProperty("accountManager").GetProperty("id").GetGuid());
            Assert.Equal(manager, (await editor.JsonAsync($"{ClinicsPath}/{id}")).GetProperty("accountManager").GetProperty("id").GetGuid());
            Assert.Equal(1, await sql.CountAsync("audit.PlatformEntry", $"TargetId = '{id}' AND ActionCode = 'clinic.account-manager-assigned'"));
            Assert.Equal(1, await sql.CountAsync("audit.Entry", $"ClinicId = '{id}' AND ActionCode = 'clinic.account-manager-assigned' AND ActorKind = 'operator'"));

            // A stale version changes nothing.
            Assert.Equal((409, "CLINIC_CHANGED_ELSEWHERE"), await editor.OpsPutAsync($"{ClinicsPath}/{id}/account-manager", new { version, operatorId = (Guid?)null }).RefusalAsync());

            var fresh = after.GetProperty("version").GetString();
            Assert.Equal((409, "OPERATOR_NOT_ACTIVE"), await editor.OpsPutAsync($"{ClinicsPath}/{id}/account-manager", new { version = fresh, operatorId = Guid.NewGuid() }).RefusalAsync());
            Assert.Equal(200, (await editor.OpsPutAsync($"{ClinicsPath}/{id}/account-manager", new { version = fresh, operatorId = (Guid?)null }).RefusalAsync()).Status);
            Assert.Equal(JsonValueKind.Null, (await editor.JsonAsync($"{ClinicsPath}/{id}")).GetProperty("accountManager").ValueKind);
        }
    }

    [Fact]
    public async Task The_operator_page_counts_the_clinics_an_operator_looks_after()
    {
        var (world, editor, id, _) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var version = (await editor.JsonAsync($"{ClinicsPath}/{id}")).GetProperty("version").GetString();
            await editor.OpsPutAsync($"{ClinicsPath}/{id}/account-manager", new { version, operatorId = world.OperatorId });
            using var admin = await world.Factory.SignInWithAsync(OperatorPermissions.OperatorsView, OperatorPermissions.ClinicsView);

            var page = await admin.JsonAsync($"{TeamHelp.Operators}/{world.OperatorId}");
            var managed = page.GetProperty("performance").EnumerateArray().Single(c => c.GetProperty("key").GetString() == "clinicsManaged");
            Assert.Equal(1, managed.GetProperty("value").GetInt32());
        }
    }

    [Fact]
    public async Task The_invitation_is_sent_again_and_refused_within_a_minute_when_unsent_or_accepted()
    {
        var (world, editor, id, email) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var path = $"{ClinicsPath}/{id}/invitation";
            var firstToken = world.Factory.Mail.LatestLinkTokenFor(email);

            Assert.Equal((429, "RESET_EMAIL_COOLDOWN"), await editor.OpsPostAsync(path).RefusalAsync());

            // Signed in before the clock moves: a role dated ahead would sort after every later test's.
            using var again = await world.Factory.SignInWithAsync(Editor);
            using var third = await world.Factory.SignInWithAsync(Editor);
            using var fourth = await world.Factory.SignInWithAsync(Editor);

            world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
            var sent = await again.OpsPostAsync(path).AnswerAsync();
            Assert.Equal(200, sent.Status);
            Assert.Equal(2, world.Factory.Mail.LinksSentTo(email));
            Assert.Equal(1, await sql.CountAsync("audit.PlatformEntry", $"TargetId = '{id}' AND ActionCode = 'clinic.invitation-resent'"));

            // The earlier link stops working.
            using var browser = world.Factory.Browser();
            Assert.NotEqual(200, (int)(await browser.InspectLinkAsync(firstToken)).StatusCode);

            // An email that did not leave is never shown as sent, and the earlier link still works.
            world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
            world.Factory.Mail.FailingFor[email] = true;
            Assert.Equal((502, "ACCOUNT_LINK_NOT_SENT"), await third.OpsPostAsync(path).RefusalAsync());
            world.Factory.Mail.FailingFor.TryRemove(email, out _);
            var liveToken = world.Factory.Mail.LatestLinkTokenFor(email);
            Assert.Equal(200, (int)(await browser.InspectLinkAsync(liveToken)).StatusCode);

            // Accepted: nothing to send.
            Assert.True((await browser.SetWithLinkAsync(liveToken, SignIn.Password)).IsSuccessStatusCode);
            world.Factory.Clock.Advance(TimeSpan.FromSeconds(61));
            Assert.Equal((409, "INVITATION_ALREADY_ACCEPTED"), await fourth.OpsPostAsync(path).RefusalAsync());
        }
    }

    [Fact]
    public async Task The_settings_tab_reads_the_general_settings_and_the_permission()
    {
        var (world, editor, id, _) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            using var viewer = await world.Factory.SignInWithAsync(Viewer);

            var forEditor = await editor.JsonAsync($"{ClinicsPath}/{id}/settings");
            var forViewer = await viewer.JsonAsync($"{ClinicsPath}/{id}/settings");

            Assert.Equal("ar", forEditor.GetProperty("defaultLanguage").GetString());
            Assert.Equal("Africa/Cairo", forEditor.GetProperty("timeZone").GetString());
            Assert.Equal("https://app.scs-heta.com", forEditor.GetProperty("staffAddress").GetString());
            Assert.True(forEditor.GetProperty("can").GetProperty("edit").GetBoolean());
            Assert.False(forViewer.GetProperty("can").GetProperty("edit").GetBoolean());
        }
    }
}
