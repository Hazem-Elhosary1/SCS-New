using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, SC-006 and FR-051: whatever path is taken — inviting, editing an operator,
/// creating, editing or copying a role — nobody ends up holding a permission that the person who
/// gave it did not hold; and what those paths write to the logs names nobody.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class NobodyGainsTests(SqlServerFixture sql)
{
    private static readonly string[] Subset =
    [
        OperatorPermissions.OperatorsManage, OperatorPermissions.OperatorsManageRoles, OperatorPermissions.TicketsReply,
        OperatorPermissions.BillingView,
    ];

    private static Guid IdOf(string text) => JsonDocument.Parse(text).RootElement.GetProperty("id").GetGuid();

    /// <summary>What every account and every role holds that the caller did not, among those the caller touched.</summary>
    private async Task<List<string>> BeyondAsync(IEnumerable<Guid> operators, IEnumerable<Guid> roles, HashSet<string> held)
    {
        var accounts = string.Join(", ", operators.Select(id => $"'{id}'").DefaultIfEmpty("NULL"));
        var touched = string.Join(", ", roles.Select(id => $"'{id}'").DefaultIfEmpty("NULL"));
        var codes = await sql.OpsColumnAsync(
            $"""
             SELECT DISTINCT p.Code FROM [ops].[RolePermission] p
             WHERE p.RoleId IN ({touched}) OR p.RoleId IN (SELECT RoleId FROM [ops].[Operator] WHERE Id IN ({accounts}))
             """);
        return [.. codes.Where(code => !held.Contains(code))];
    }

    [Fact]
    public async Task By_no_path_does_anyone_come_to_hold_what_the_giver_did_not()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (_, email, ownRole) = await world.Factory.CreateOperatorWithAsync(Subset);
        using var caller = await world.Factory.SignInOperatorAsync(email);
        var held = OperatorPermissions.WithRequired(Subset).ToHashSet(StringComparer.Ordinal);

        var wide = await world.Factory.CreateRoleAsync(OperatorPermissions.SettingsEdit, OperatorPermissions.TicketsReply);
        var narrow = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsReply);
        var (target, _) = await world.Factory.CreateOperatorAsync(roleId: narrow);
        var made = new List<Guid>();
        var roles = new List<Guid> { narrow, ownRole };

        // Inviting: every role there is, one after another. Only those within the caller's own go through.
        var options = (await caller.JsonAsync($"{TeamHelp.Roles}/options")).GetProperty("items").EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid()).ToList();
        foreach (var role in options.Take(12).Append(wide).Append(TeamHelp.SuperAdmin).Distinct())
        {
            var (status, _, text) = await caller.OpsPostAsync(TeamHelp.Operators, TeamHelp.NewOperator(role)).AnswerAsync();
            if (status == 201)
            {
                made.Add(IdOf(text));
            }
        }

        Assert.NotEmpty(made);

        // Editing an operator onto each of them.
        foreach (var role in new[] { wide, TeamHelp.SuperAdmin, TeamHelp.BillingAdmin, TeamHelp.Viewer, ownRole })
        {
            var body = (await caller.EditBodyAsync(target)).With("roleId", role);
            await caller.OpsPutAsync($"{TeamHelp.Operators}/{target}", body).AnswerAsync();
        }

        // Creating roles: with a code beyond, with everything, and by copying a wider role.
        foreach (var codes in new[]
                 {
                     [OperatorPermissions.TicketsReply, OperatorPermissions.SettingsEdit],
                     [.. OperatorPermissions.All.Select(p => p.Code)],
                     new[] { OperatorPermissions.TicketsReply },
                 })
        {
            var (status, _, text) = await caller.OpsPostAsync(
                TeamHelp.Roles, new { name = $"نطاق {Guid.NewGuid():N}"[..20], codes, basedOnId = wide }).AnswerAsync();
            if (status == 201)
            {
                roles.Add(IdOf(text));
            }
        }

        var copy = (await caller.JsonAsync($"{TeamHelp.Roles}/{wide}")).GetProperty("copy").GetProperty("codes").EnumerateArray()
            .Select(c => c.GetString()!).ToArray();
        var (copied, _, copiedText) = await caller.OpsPostAsync(
            TeamHelp.Roles, new { name = $"نسخة {Guid.NewGuid():N}"[..20], codes = copy, basedOnId = wide }).AnswerAsync();
        Assert.Equal(201, copied);
        roles.Add(IdOf(copiedText));

        // Editing a role they made, to add what they lack.
        var mine = roles[^1];
        var role1 = await caller.JsonAsync($"{TeamHelp.Roles}/{mine}");
        await caller.OpsPutAsync(
            $"{TeamHelp.Roles}/{mine}",
            new
            {
                name = role1.GetProperty("name").GetString(),
                codes = OperatorPermissions.All.Select(p => p.Code).ToArray(),
                version = role1.GetProperty("version").GetString(),
            }).AnswerAsync();

        // Whoever they invited or edited, and every role they made or edited, holds only what they held.
        Assert.Empty(await BeyondAsync(made.Append(target), roles, held));

        // And the caller themselves gained nothing on the way.
        var session = await caller.JsonAsync("/v1/ops/auth/session");
        Assert.Equal(
            OperatorPermissions.InOrder(held),
            session.GetProperty("permissions").EnumerateArray().Select(c => c.GetString()!));
    }

    [Fact]
    public async Task What_these_paths_write_to_the_logs_names_nobody()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();
        const string Name = "Layla Unmistakable";
        const string Reason = "left-the-team-unmistakably";
        const string Note = "merged-into-another-unmistakably";

        var body = TeamHelp.NewOperator(TeamHelp.Viewer).With("fullName", Name).With("mobile", "+20 100 987 6543");
        var email = (string)body["email"]!;
        var (_, _, created) = await admin.OpsPostAsync(TeamHelp.Operators, body).AnswerAsync();
        var id = IdOf(created);
        await world.Factory.SendInvitationsAsync();
        var token = world.Factory.LatestLinkToken(email);

        var edit = (await admin.EditBodyAsync(id)).With("jobTitle", "Unmistakable Title").With("note", Note);
        Assert.Equal(204, (await admin.OpsPutAsync($"{TeamHelp.Operators}/{id}", edit).AnswerAsync()).Status);

        var (active, activeEmail) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        Assert.Equal(202, (await admin.OpsPostAsync($"{TeamHelp.Operators}/{active}/password-link").AnswerAsync()).Status);
        var reset = world.Factory.LatestLinkToken(activeEmail);
        Assert.Equal(204, (await admin.OpsPostAsync($"{TeamHelp.Operators}/{active}/suspend", new { reason = Reason }).AnswerAsync()).Status);

        var role = await world.Factory.CreateRoleAsync(OperatorPermissions.TicketsView);
        Assert.Equal(204, (await admin.OpsPostAsync($"{TeamHelp.Roles}/{role}/delete", new { note = Note }).AnswerAsync()).Status);

        // A link that could not leave is logged by the account's identifier alone.
        var (unlucky, unluckyEmail) = await world.Factory.CreateOperatorAsync(roleId: TeamHelp.Viewer);
        world.Factory.Mail.FailingFor[unluckyEmail] = true;
        Assert.Equal(503, (await admin.OpsPostAsync($"{TeamHelp.Operators}/{unlucky}/password-link").AnswerAsync()).Status);

        var logs = world.Factory.Logs.All;
        foreach (var secret in new[] { Name, email, activeEmail, unluckyEmail, token, reset, Reason, Note, "Unmistakable Title", "100 987 6543" })
        {
            Assert.DoesNotContain(secret, logs, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(world.Factory.Logs.Lines, line => line.Contains(unlucky.ToString(), StringComparison.OrdinalIgnoreCase));
    }
}
