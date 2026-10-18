using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using static Scs.IntegrationTests.Fixtures.ClinicFixtures;

namespace Scs.IntegrationTests.Clinics;

/// <summary>
/// Spec 015, FR-004 and the edge case "the users limit of the chosen plan is 0" (SH-017): the
/// owner's account takes a seat, so an add on such a plan is refused with the plan's limit — before
/// anything is written. Test-first: plan limits (constitution VII).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AddClinicPlanLimitTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_plan_with_no_seat_refuses_the_add_and_leaves_nothing()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        await using var built = await sql.BuiltAsync("max_users");

        // A plan's own limit is 1 or more; a plan that leaves the users out has the catalogue's
        // baseline, which an operator may set to 0 (spec 012) — the only way a plan has no seat.
        var before = await TestDatabase.ScalarAsync<int?>(sql.AdminConnection, "SELECT Baseline FROM plans.Feature WHERE [Key] = 'max_users'");
        await sql.ExecuteAsync(sql.AdminConnection, "UPDATE plans.Feature SET Baseline = 0 WHERE [Key] = 'max_users'");
        try
        {
            await RefusedAsync(world);
        }
        finally
        {
            var restore = before is null ? "NULL" : before.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await sql.ExecuteAsync(sql.AdminConnection, $"UPDATE plans.Feature SET Baseline = {restore} WHERE [Key] = 'max_users'");
        }
    }

    private async Task RefusedAsync(OpsWorld world)
    {
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var email = NewEmail();
        var nameEn = $"No Seat {Guid.NewGuid():N}"[..24];

        var refusal = await editor.OpsPostAsync(ClinicsPath, NewClinic(plan, nameEn, email)).RefusalAsync();

        Assert.Equal((409, "PLAN_LIMIT_REACHED"), refusal);
        Assert.Equal(0, await sql.LeftoversAsync(email, nameEn));
    }

    [Fact]
    public async Task A_plan_with_room_for_the_owner_lets_the_add_through()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        await using var built = await sql.BuiltAsync("max_users");
        var plan = await world.Factory.PutPlanAsync(grid: [("max_users", 1)]);
        using var editor = await world.Factory.SignInWithAsync(Editor);

        var (id, _) = await editor.AddClinicAsync(NewClinic(plan));

        Assert.Equal(1, await sql.CountAsync("[identity].StaffAccount", $"ClinicId = '{id}'"));
    }
}
