using Microsoft.Extensions.DependencyInjection;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.Plans.Contracts;
using Scs.SharedKernel.Access;
using static Scs.IntegrationTests.Fixtures.ClinicFixtures;
using static Scs.IntegrationTests.Fixtures.PlanFixtures;

namespace Scs.IntegrationTests.Clinics;

/// <summary>
/// Spec 015, User Story 6 (FR-029): 013's plan screens count real clinics — on the plan, new this
/// month, on trial — list them with their status, and show revenue 0 until spec 018 gives clinics a
/// billing cycle. Clinics being set up and clinics with no plan are not counted.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlanSubscribersTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Each_plan_counts_its_clinics_and_lists_them_with_their_status()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var first = await world.Factory.PutPlanAsync();
        var second = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var (a, _) = await editor.AddClinicAsync(NewClinic(first));
        var (b, _) = await editor.AddClinicAsync(NewClinic(first));
        await editor.AddClinicAsync(NewClinic(second));
        var (hidden, _) = await editor.AddClinicAsync(NewClinic(first));
        await sql.ExecuteAsync(sql.Main.Crossing, $"UPDATE tenancy.Clinic SET State = 'setting-up' WHERE Id = '{hidden}'");
        await sql.ExecuteAsync(sql.Main.Crossing, $"UPDATE subscriptions.ClinicPlan SET TrialEndsAtUtc = DATEADD(day, -1, SYSDATETIMEOFFSET()) WHERE ClinicId = '{b}'");

        using var scope = world.Factory.Services.CreateScope();
        var subscribers = scope.ServiceProvider.GetRequiredService<IPlanSubscribers>();
        var counts = await subscribers.CountsAsync([first, second], CancellationToken.None);

        Assert.Equal(new PlanSubscriberCounts(2, 2, 1, 0m), counts[first]);
        Assert.Equal(new PlanSubscriberCounts(1, 1, 1, 0m), counts[second]);
        Assert.Equal(0m, (await subscribers.RevenueAsync(CancellationToken.None))!.Now);

        var listed = await subscribers.ClinicsAsync(first, CancellationToken.None);
        Assert.Equal([b, a], listed.Select(c => c.ClinicId));
        Assert.Equal(["نشطة", "تجربة"], listed.Select(c => c.StateAr));
        // Only the clinic in trial says when its trial ends (T074).
        Assert.Equal([false, true], listed.Select(c => c.TrialEndsAtUtc.HasValue));
    }

    [Fact]
    public async Task The_plans_screens_show_the_real_numbers()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var (id, _) = await editor.AddClinicAsync(NewClinic(plan));
        using var billing = await world.Factory.SignInWithAsync(OperatorPermissions.BillingView);

        var detail = await billing.JsonAsync($"{PlansPath}/{plan}");

        Assert.Contains(id.ToString(), detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }
}
