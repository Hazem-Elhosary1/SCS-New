using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Tenancy;
using Scs.ClinicData.Contracts;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using Scs.Subscriptions.Contracts;
using Scs.Tenancy.Contracts;
using static Scs.IntegrationTests.Fixtures.ClinicFixtures;

namespace Scs.IntegrationTests.Clinics;

/// <summary>
/// Spec 015, FR-004 and the edge case "the add fails part-way" (research R2): a fault after the
/// checks — at the branch, at the plan, at activation — is undone as far as it can be, and nothing
/// of it is ever visible: no list row, no detail, no locator answer, and the email free again.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AddClinicFaultTests(SqlServerFixture sql)
{
    public static TheoryData<string> Stages => new() { "branch", "plan", "activate" };

    [Theory]
    [MemberData(nameof(Stages))]
    public async Task A_fault_after_the_checks_leaves_nothing_visible_and_the_email_free(string stage)
    {
        using var world = await OpsWorld.CreateAsync(sql, configure: factory => factory.WithServices(services => Break(services, stage)));
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var email = NewEmail();
        var nameEn = $"Broken {Guid.NewGuid():N}"[..22];

        var (status, _, _) = await editor.OpsPostAsync(ClinicsPath, NewClinic(plan, nameEn, email)).AnswerAsync();

        Assert.Equal(500, status);
        var id = await sql.ValueAsync<Guid>($"SELECT Id FROM tenancy.Clinic WHERE NameEn = N'{nameEn}'");
        Assert.Equal("discarded", await sql.ValueAsync<string>($"SELECT State FROM tenancy.Clinic WHERE Id = '{id}'"));
        Assert.Equal(0, await sql.CountAsync("tenancy.ClinicDirectory", $"ClinicId = '{id}' AND IsCurrent = 1"));
        Assert.Equal(0, await sql.CountAsync("[identity].StaffAccount", $"Email = N'{email}'"));
        // A branch made before the fault is undone too (SC-003; T077), and the plan link (T090).
        Assert.Equal(0, await sql.CountAsync("clinicdata.Branch", $"ClinicId = '{id}'"));
        Assert.Equal(0, await sql.CountAsync("subscriptions.ClinicPlan", $"ClinicId = '{id}'"));
        Assert.Equal(0, await sql.CountAsync("audit.PlatformEntry", $"TargetId = '{id}' AND ActionCode = 'clinic.created'"));

        Assert.Equal((404, "NOT_FOUND"), await editor.OpsGetAsync($"{ClinicsPath}/{id}").RefusalAsync());
        var list = await editor.JsonAsync($"{ClinicsPath}?search={Uri.EscapeDataString(nameEn)}");
        Assert.Equal(0, list.GetProperty("total").GetInt32());

        using var scope = world.Factory.Services.CreateScope();
        var located = await scope.ServiceProvider.GetRequiredService<IClinicLocator>()
            .LocateAsync(nameEn.ToLowerInvariant().Replace(' ', '-'), CancellationToken.None);
        Assert.Null(located);
    }

    [Fact]
    public async Task A_fault_inside_the_real_activation_still_discards_the_clinic()
    {
        // Research R2: a fault within activation itself, after the clinic was marked active in
        // memory, still discards it and retires its slug (T097). A check of this test's database
        // refuses the activation's platform entry for clinics named for it, after the clinic's row
        // was saved inside the activation's transaction.
        await sql.EnsureMigratedAsync();
        await sql.ExecuteAsync(sql.AdminConnection, """
            ALTER TABLE audit.PlatformEntry WITH NOCHECK ADD CONSTRAINT CK_Test_RefusesActivation
                CHECK (NOT (ActionCode = 'clinic.created' AND Facts LIKE N'%unactivated-%'));
            """);
        try
        {
            using var world = await OpsWorld.CreateAsync(sql);
            var plan = await world.Factory.PutPlanAsync();
            using var editor = await world.Factory.SignInWithAsync(Editor);
            var email = NewEmail();
            var nameEn = $"Unactivated {Guid.NewGuid():N}"[..26];

            var (status, _, _) = await editor.OpsPostAsync(ClinicsPath, NewClinic(plan, nameEn, email)).AnswerAsync();

            Assert.Equal(500, status);
            var id = await sql.ValueAsync<Guid>($"SELECT Id FROM tenancy.Clinic WHERE NameEn = N'{nameEn}'");
            Assert.Equal("discarded", await sql.ValueAsync<string>($"SELECT State FROM tenancy.Clinic WHERE Id = '{id}'"));
            Assert.Equal(0, await sql.CountAsync("tenancy.ClinicDirectory", $"ClinicId = '{id}' AND IsCurrent = 1"));
            Assert.Equal(0, await sql.CountAsync("[identity].StaffAccount", $"Email = N'{email}'"));
        }
        finally
        {
            await sql.ExecuteAsync(sql.AdminConnection, "ALTER TABLE audit.PlatformEntry DROP CONSTRAINT IF EXISTS CK_Test_RefusesActivation;");
        }
    }

    [Fact]
    public async Task A_request_cancelled_part_way_is_undone_like_a_fault()
    {
        // The edge case "the add fails part-way" and research R2: a dropped connection after the
        // owner was made leaves no account, so the email is free again (T093).
        using var world = await OpsWorld.CreateAsync(sql, configure: factory => factory.WithServices(services =>
            services.AddScoped<IBranches, CancelledBranches>()));
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var email = NewEmail();
        var nameEn = $"Cancelled {Guid.NewGuid():N}"[..24];

        var (status, _, _) = await editor.OpsPostAsync(ClinicsPath, NewClinic(plan, nameEn, email)).AnswerAsync();

        Assert.NotEqual(201, status);
        Assert.Equal("discarded", await sql.ValueAsync<string>($"SELECT State FROM tenancy.Clinic WHERE NameEn = N'{nameEn}'"));
        Assert.Equal(0, await sql.CountAsync("[identity].StaffAccount", $"Email = N'{email}'"));
    }

    [Fact]
    public async Task A_fault_while_inviting_leaves_the_clinic_made_and_answers_its_address()
    {
        // FR-004, US1 scenario 6: the clinic is whole before the invitation; a fault there is not
        // "the clinic could not be created" (T086).
        using var world = await OpsWorld.CreateAsync(sql, configure: factory => factory.WithServices(services =>
            services.AddScoped<Scs.Identity.Contracts.IAccountHandover, BrokenInvitation>()));
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);

        var (id, _) = await editor.AddClinicAsync(NewClinic(plan));

        Assert.Equal("active", await sql.ValueAsync<string>($"SELECT State FROM tenancy.Clinic WHERE Id = '{id}'"));
        Assert.Equal("none", (await editor.JsonAsync($"{ClinicsPath}/{id}")).GetProperty("owner").GetProperty("invitation").GetProperty("state").GetString());
    }

    [Fact]
    public async Task After_a_fault_the_same_email_adds_a_clinic()
    {
        var email = NewEmail();
        using (var broken = await OpsWorld.CreateAsync(sql, configure: factory => factory.WithServices(services => Break(services, "plan"))))
        {
            var plan = await broken.Factory.PutPlanAsync();
            using var editor = await broken.Factory.SignInWithAsync(Editor);
            Assert.Equal(500, (await editor.OpsPostAsync(ClinicsPath, NewClinic(plan, email: email)).RefusalAsync()).Status);
        }

        using var world = await OpsWorld.CreateAsync(sql);
        var again = await world.Factory.PutPlanAsync();
        using var fixedEditor = await world.Factory.SignInWithAsync(Editor);
        await fixedEditor.AddClinicAsync(NewClinic(again, email: email));
    }

    private static void Break(IServiceCollection services, string stage)
    {
        switch (stage)
        {
            case "branch":
                services.AddScoped<IBranches, BrokenBranches>();
                break;
            case "plan":
                services.AddScoped<IClinicPlans, BrokenPlans>();
                break;
            default:
                services.AddScoped<IClinicRegistry>(provider =>
                    new BrokenActivation(ActivatorUtilities.CreateInstance<Scs.Tenancy.Infrastructure.ClinicRegistry>(provider)));
                break;
        }
    }

    private sealed class BrokenBranches : IBranches
    {
        public Task<Guid> CreateMainAsync(string nameAr, string nameEn, string address, CancellationToken cancellation) =>
            throw new InvalidOperationException("The branch's database is down (test).");

        public Task UndoMainAsync(Guid branchId, CancellationToken cancellation) => Task.CompletedTask;

        public Task<int> CountAsync(CancellationToken cancellation) => Task.FromResult(0);
    }

    /// <summary>The request is given up while the branch is being made.</summary>
    private sealed class CancelledBranches : IBranches
    {
        public Task<Guid> CreateMainAsync(string nameAr, string nameEn, string address, CancellationToken cancellation) =>
            throw new OperationCanceledException("The operator's connection dropped (test).");

        public Task UndoMainAsync(Guid branchId, CancellationToken cancellation) => Task.CompletedTask;

        public Task<int> CountAsync(CancellationToken cancellation) => Task.FromResult(0);
    }

    private sealed class BrokenPlans : IClinicPlans
    {
        public Task<Guid?> CurrentAsync(CancellationToken cancellation) => Task.FromResult<Guid?>(null);

        public Task SetAsync(Guid? planId, CancellationToken cancellation) => Task.CompletedTask;

        public Task StartAsync(Guid planId, DateTimeOffset trialEndsAtUtc, CancellationToken cancellation) =>
            throw new InvalidOperationException("The plan's database is down (test).");

        public Task<ClinicPlanState?> StateAsync(CancellationToken cancellation) => Task.FromResult<ClinicPlanState?>(null);

        public Task UndoStartAsync(CancellationToken cancellation) => Task.CompletedTask;
    }

    /// <summary>An invitation step whose database fails: every other handover is not used by the add.</summary>
    private sealed class BrokenInvitation : Scs.Identity.Contracts.IAccountHandover
    {
        public Task<Scs.Identity.Contracts.AccountHandoverResult> ResendInvitationAsync(Guid accountId, CancellationToken cancellation) =>
            throw new InvalidOperationException("The links' database is down (test).");

        public Task<Scs.Identity.Contracts.AccountHandoverResult> CreateWithLinkAsync(string email, CancellationToken cancellation) =>
            throw new NotSupportedException();

        public Task<Scs.Identity.Contracts.AccountHandoverResult> CreateWithGeneratedPasswordAsync(string email, CancellationToken cancellation) =>
            throw new NotSupportedException();

        public Task<Scs.Identity.Contracts.AccountHandoverResult> CreateWithTypedPasswordAsync(string email, string password, CancellationToken cancellation) =>
            throw new NotSupportedException();

        public Task<Scs.Identity.Contracts.AccountHandoverResult> ReissueAsync(Guid accountId, string method, string? password, CancellationToken cancellation) =>
            throw new NotSupportedException();
    }

    /// <summary>The real registry, except that activation fails.</summary>
    private sealed class BrokenActivation(IClinicRegistry real) : IClinicRegistry
    {
        public Task ActivateAsync(Guid clinicId, ClinicCreation creation, CancellationToken cancellation) =>
            throw new InvalidOperationException("Activation failed (test).");

        public Task<Guid> RegisterAsync(string shortName, string nameAr, string nameEn, string operatorId, CancellationToken cancellation) =>
            real.RegisterAsync(shortName, nameAr, nameEn, operatorId, cancellation);

        public Task<IAsyncDisposable> TakeAddTurnAsync(CancellationToken cancellation) => real.TakeAddTurnAsync(cancellation);

        public Task<RegisteredClinic> RegisterAsync(ClinicRegistration registration, CancellationToken cancellation) =>
            real.RegisterAsync(registration, cancellation);

        public Task DiscardAsync(Guid clinicId, string operatorId, CancellationToken cancellation) =>
            real.DiscardAsync(clinicId, operatorId, cancellation);

        public Task CheckProfileAsync(
            Guid clinicId, byte[] version, ClinicProfileChange change, Dictionary<string, string[]> errors, CancellationToken cancellation) =>
            real.CheckProfileAsync(clinicId, version, change, errors, cancellation);

        public Task<byte[]> UpdateProfileAsync(
            Guid clinicId, byte[] version, ClinicProfileChange change, bool ownerChanged, CancellationToken cancellation) =>
            real.UpdateProfileAsync(clinicId, version, change, ownerChanged, cancellation);

        public Task<byte[]> AssignAccountManagerAsync(
            Guid clinicId, byte[] version, Guid? accountManagerId, string? nameBefore, string? nameAfter, string operatorId,
            CancellationToken cancellation) =>
            real.AssignAccountManagerAsync(clinicId, version, accountManagerId, nameBefore, nameAfter, operatorId, cancellation);

        public Task RenameAsync(Guid clinicId, string newShortName, string operatorId, CancellationToken cancellation) =>
            real.RenameAsync(clinicId, newShortName, operatorId, cancellation);

        public Task<IReadOnlyList<ClinicListItem>> ListAsync(string operatorId, CancellationToken cancellation) =>
            real.ListAsync(operatorId, cancellation);

        public Task<IReadOnlyList<Guid>> ListClinicIdsForJobsAsync(CancellationToken cancellation) =>
            real.ListClinicIdsForJobsAsync(cancellation);
    }
}
