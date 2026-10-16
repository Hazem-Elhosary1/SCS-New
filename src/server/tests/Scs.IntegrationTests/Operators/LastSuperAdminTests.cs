using Scs.IntegrationTests.Fixtures;
using Scs.Operators.Domain;

namespace Scs.IntegrationTests.Operators;

/// <summary>
/// Spec 011, FR-038 and SC-005 (ق-56; research R5): one active «مسؤول عام» always remains,
/// whatever arrives at the same moment.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class LastSuperAdminTests(SqlServerFixture sql)
{
    // The guard itself, or — when the other change ended this one's session first — the gate.
    private static readonly int[] Refusals = [409, 401, 403];

    private static string Path(Guid id) => $"{TeamHelp.Operators}/{id}";

    /// <summary>
    /// The database is shared by the tests that ran before this one: every super admin they left
    /// is made a viewer, so that the ones named are the only ones.
    /// </summary>
    private static Task OnlyTheseAreSuperAdminsAsync(OpsWorld world, params Guid[] kept) =>
        world.Factory.OpsSqlAsync(
            $"""
             UPDATE [ops].[Operator] SET [RoleId] = '{TeamHelp.Viewer}'
             WHERE [RoleId] = '{TeamHelp.SuperAdmin}' AND [Id] NOT IN ({string.Join(", ", kept.Select(id => $"'{id}'"))})
             """);

    private Task<List<string>> ActiveSuperAdminsAsync() => sql.OpsColumnAsync(
        $"SELECT CAST(Id AS varchar(36)) FROM [ops].[Operator] WHERE RoleId = '{TeamHelp.SuperAdmin}' AND State = 'active'");

    [Fact]
    public async Task The_only_active_super_admin_keeps_the_role_and_is_not_suspended()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        using var admin = await world.SignInAsync();

        // Someone else who manages operators, and two super admins who do not count: one invited, one suspended.
        var (_, managerEmail, _) = await world.Factory.CreateOperatorWithAsync(Scs.SharedKernel.Access.OperatorPermissions.OperatorsManage);
        using var manager = await world.Factory.SignInOperatorAsync(managerEmail);
        var (invited, _) = await world.Factory.CreateOperatorAsync(OperatorStates.Invited);
        var (suspended, _) = await world.Factory.CreateOperatorAsync(OperatorStates.Suspended);
        await OnlyTheseAreSuperAdminsAsync(world, world.OperatorId, invited, suspended);

        var body = await manager.EditBodyAsync(world.OperatorId);
        var custom = await world.Factory.CreateRoleAsync(Scs.SharedKernel.Access.OperatorPermissions.OperatorsView);
        var (status, code, _) = await manager.OpsPutAsync(Path(world.OperatorId), body.With("roleId", custom)).AnswerAsync();
        Assert.Equal((409, "OPS_LAST_SUPER_ADMIN"), (status, code));

        var (suspend, suspendCode, _) = await manager.OpsPostAsync($"{Path(world.OperatorId)}/suspend", new { }).AnswerAsync();
        Assert.Equal((409, "OPS_LAST_SUPER_ADMIN"), (suspend, suspendCode));

        Assert.Equal([world.OperatorId.ToString()], await ActiveSuperAdminsAsync(), StringComparer.OrdinalIgnoreCase);
        Assert.Empty(await sql.PlatformEntriesAsync(world.OperatorId));

        // A super admin who is not active may be moved: that leaves the count as it was.
        var theirs = await admin.EditBodyAsync(suspended);
        Assert.Equal(204, (await admin.OpsPutAsync(Path(suspended), theirs.With("roleId", TeamHelp.Viewer)).AnswerAsync()).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Two_changes_at_the_same_moment_leave_one_refused_and_one_super_admin_active(bool secondSuspends)
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var (other, otherEmail) = await world.Factory.CreateOperatorAsync();
        await OnlyTheseAreSuperAdminsAsync(world, world.OperatorId, other);

        // Each acts on the other, so neither is acting on their own account.
        using var first = await world.SignInAsync();
        using var second = await world.Factory.SignInOperatorAsync(otherEmail);
        var demoteOther = (await first.EditBodyAsync(other)).With("roleId", TeamHelp.Viewer);
        var demoteFirst = (await second.EditBodyAsync(world.OperatorId)).With("roleId", TeamHelp.Viewer);

        var one = first.OpsPutAsync(Path(other), demoteOther).AnswerAsync();
        var two = secondSuspends
            ? second.OpsPostAsync($"{Path(world.OperatorId)}/suspend", new { reason = "في اللحظة نفسها" }).AnswerAsync()
            : second.OpsPutAsync(Path(world.OperatorId), demoteFirst).AnswerAsync();
        var answers = await Task.WhenAll(one, two);

        // One went through. The other found either the guard, or that it had been signed out by the first.
        Assert.Single(answers, a => a.Status == 204);
        var refused = Assert.Single(answers, a => a.Status != 204);
        Assert.Contains(refused.Status, Refusals);
        if (refused.Status == 409)
        {
            Assert.Equal("OPS_LAST_SUPER_ADMIN", refused.Code);
        }

        Assert.Single(await ActiveSuperAdminsAsync());
    }
}
