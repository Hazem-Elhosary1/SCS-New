using System.Net;
using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, User Story 1 (FR-003, FR-005; contracts/operator-access.md §1–§3): a route of the
/// operators console answers only for an operator whose role holds its permission at that
/// moment. Written before the gate.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OperatorGateTests(SqlServerFixture sql)
{
    private const string Needs = "/probe/ops/needs/";

    private static async Task<(int Status, string? Code, string? Permission)> ReadRefusalAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (text.Length == 0 || text[0] != '{')
        {
            return ((int)response.StatusCode, null, null);
        }

        using var body = JsonDocument.Parse(text);
        return (
            (int)response.StatusCode,
            body.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null,
            body.RootElement.TryGetProperty("permission", out var permission) ? permission.GetString() : null);
    }

    [Fact]
    public async Task Nobody_without_an_operators_session_is_let_in()
    {
        using var world = await OpsWorld.CreateAsync(sql);

        using var anonymous = world.Factory.Browser();
        using var none = await anonymous.OpsGetAsync(Needs + OperatorPermissions.OperatorsView);
        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);

        // A clinic's session is no session here.
        var clinic = await sql.Main.SeedClinicAsync();
        var (_, staffEmail) = await world.Factory.CreateAccountAsync(clinic);
        using var staff = await world.Factory.SignInAsAsync(staffEmail, SignIn.Password);
        using var crossed = await staff.OpsGetAsync(Needs + OperatorPermissions.OperatorsView);
        Assert.Equal(HttpStatusCode.Unauthorized, crossed.StatusCode);

        using var ownCrossed = await staff.OpsGetAsync("/probe/ops/own");
        Assert.Equal(HttpStatusCode.Unauthorized, ownCrossed.StatusCode);
    }

    [Fact]
    public async Task An_operator_is_served_what_their_role_holds_and_refused_the_rest_before_anything_runs()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (_, email, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.OperatorsManage);
        using var browser = await world.Factory.SignInOperatorAsync(email);

        // Held, and the view it needs.
        using var held = await browser.OpsGetAsync(Needs + OperatorPermissions.OperatorsManage);
        using var needed = await browser.OpsGetAsync(Needs + OperatorPermissions.OperatorsView);
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (held.StatusCode, needed.StatusCode));

        // Not held: refused naming the code, and the handler did not run.
        var before = ProbeEndpoints.OpsRuns.GetValueOrDefault(OperatorPermissions.SettingsEdit);
        using var refused = await browser.OpsGetAsync(Needs + OperatorPermissions.SettingsEdit);
        Assert.Equal((403, "PERMISSION_REQUIRED", OperatorPermissions.SettingsEdit), await ReadRefusalAsync(refused));
        Assert.Equal(before, ProbeEndpoints.OpsRuns.GetValueOrDefault(OperatorPermissions.SettingsEdit));

        // What an operator does to their own account needs no permission at all.
        using var own = await browser.OpsGetAsync("/probe/ops/own");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task The_refusal_is_the_same_whether_or_not_the_record_exists()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (_, email, _) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.OperatorsView);
        using var browser = await world.Factory.SignInOperatorAsync(email);

        using var there = await browser.OpsGetAsync("/probe/ops/needs-record/1");
        using var notThere = await browser.OpsGetAsync("/probe/ops/needs-record/2");

        Assert.Equal(await ReadRefusalAsync(there), await ReadRefusalAsync(notThere));
        Assert.Equal(403, (int)there.StatusCode);
    }

    [Fact]
    public async Task A_change_to_a_role_reaches_its_holders_next_request_and_ends_no_session()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (id, email, roleId) = await world.Factory.CreateOperatorWithAsync(OperatorPermissions.TicketsReply);
        using var browser = await world.Factory.SignInOperatorAsync(email);

        using var first = await browser.OpsGetAsync(Needs + OperatorPermissions.TicketsReply);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The role loses it: the same session's very next request is refused, and stays signed in.
        await world.Factory.SetRoleCodesAsync(roleId, OperatorPermissions.TicketsView);
        using var lost = await browser.OpsGetAsync(Needs + OperatorPermissions.TicketsReply);
        Assert.Equal(HttpStatusCode.Forbidden, lost.StatusCode);

        // And gains another: served at once.
        await world.Factory.SetRoleCodesAsync(roleId, OperatorPermissions.TicketsView, OperatorPermissions.ReportsExport);
        using var gained = await browser.OpsGetAsync(Needs + OperatorPermissions.ReportsExport);
        Assert.Equal(HttpStatusCode.OK, gained.StatusCode);

        using var session = await browser.OpsGetAsync("/v1/ops/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var open = await world.Factory.OpsAsync(db => Task.FromResult(db.Sessions.Count(s => s.OperatorId == id && s.EndedAtUtc == null)));
        Assert.Equal(1, open);
    }

    [Fact]
    public async Task A_super_admin_holds_every_permission_and_a_viewer_only_the_views()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        foreach (var code in new[] { OperatorPermissions.SettingsEdit, OperatorPermissions.OperatorsManageRoles, OperatorPermissions.NoticesSend })
        {
            using var served = await admin.OpsGetAsync(Needs + code);
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        }

        var (_, viewerEmail) = await world.Factory.CreateOperatorAsync(roleId: OpsHelp.SystemRole(OperatorPermissions.SystemRoles.Viewer));
        using var viewer = await world.Factory.SignInOperatorAsync(viewerEmail);

        using var view = await viewer.OpsGetAsync(Needs + OperatorPermissions.OperatorsView);
        using var allTickets = await viewer.OpsGetAsync(Needs + OperatorPermissions.TicketsViewAll);
        using var write = await viewer.OpsGetAsync(Needs + OperatorPermissions.OperatorsManage);
        Assert.Equal(
            (HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Forbidden),
            (view.StatusCode, allTickets.StatusCode, write.StatusCode));
    }

    [Fact]
    public async Task A_route_that_declares_nothing_serves_no_operator()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        using var undeclared = await admin.GetAsync(new Uri("/probe/protected", UriKind.Relative));

        Assert.False(undeclared.IsSuccessStatusCode);
        Assert.Contains(undeclared.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    [Fact]
    public async Task An_operator_s_permission_never_opens_a_clinic_s_route()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        // The clinic's catalogue has its own codes; a super admin of the console holds none of them.
        using var clinicRoute = await admin.GetAsync(new Uri($"/probe/needs/{Permissions.BookingsView}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, clinicRoute.StatusCode);
    }
}
