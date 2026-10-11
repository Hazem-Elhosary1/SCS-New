using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Audit;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure.Persistence;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.IntegrationTests.Audit;

/// <summary>
/// Spec 007, User Story 2 (FR-004, FR-009, FR-014, FR-015; constitution IV): a change and its
/// entry are one unit of work, and what the entry says about who, where and when is taken by the
/// trail itself.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuditWritingTests(SqlServerFixture sql)
{
    private static Role NewRole(string name)
    {
        var role = Role.Custom(name, null, null, DateTimeOffset.UtcNow);
        role.Hold([Permissions.BookingsView]);
        return role;
    }

    private static AuditRecord Created(Role role) => new(
        AuditActions.RoleCreated,
        new AuditTarget(AuditTarget.Role, role.Id, role.Name),
        Changes: [AuditChange.OfSet("permissions", [Permissions.BookingsView], [])]);

    private Task<int> RolesNamedAsync(RefusalWorld world, string name) =>
        world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Role WHERE ClinicId = '{world.Clinic}' AND Name = N'{name}'");

    [Fact]
    public async Task A_change_and_its_entry_are_both_there()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (actor, email) = await world.AccountAsync();

        var id = await world.Factory.RolesAsAsync(
            world.Clinic, actor, roles => roles.CreateAsync("الاستقبال", null, [Permissions.BookingsView], default));

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated));
        Assert.Equal(1, await RolesNamedAsync(world, "الاستقبال"));
        Assert.Equal((id, "role", "الاستقبال"), (entry.TargetId, entry.TargetKind, entry.TargetName));
        Assert.Equal(("staff", actor, email), (entry.ActorKind, entry.ActorAccountId, entry.ActorName));
        Assert.Equal(world.Clinic, entry.ClinicId);
        Assert.Equal("users", entry.Category);
    }

    [Fact]
    public async Task When_the_entry_is_refused_the_change_is_not_saved()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        await world.Factory.ForClinicAsync(world.Clinic, async services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            var role = NewRole("لن يُحفظ");
            db.Roles.Add(role);
            db.Record(new AuditRecord("no.such-action", new AuditTarget(AuditTarget.Role, role.Id, role.Name)));

            await Assert.ThrowsAsync<AuditRefusedException>(() => db.SaveChangesAsync());
        });

        Assert.Equal(0, await RolesNamedAsync(world, "لن يُحفظ"));
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));
    }

    [Fact]
    public async Task When_the_change_fails_no_entry_remains()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        await world.Factory.ForClinicAsync(world.Clinic, async services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            var first = NewRole("مكرر");
            db.Roles.Add(first);
            db.Record(Created(first));
            await db.SaveChangesAsync();

            // The same name twice in one clinic: the database refuses the change.
            var second = NewRole("مكرر");
            db.Roles.Add(second);
            db.Record(Created(second));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });

        Assert.Equal(1, await RolesNamedAsync(world, "مكرر"));
        Assert.Single(await sql.Main.EntriesAsync(world.Clinic));
    }

    [Fact]
    public async Task When_the_callers_transaction_is_rolled_back_the_entry_goes_with_the_change()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        await world.Factory.ForClinicAsync(world.Clinic, async services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();

            var role = NewRole("تراجع");
            db.Roles.Add(role);
            db.Record(Created(role));
            await db.SaveChangesAsync();

            // Written, and visible inside the transaction that holds the change.
            Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [audit].Entry").SingleAsync());

            await transaction.RollbackAsync();
        });

        Assert.Equal(0, await RolesNamedAsync(world, "تراجع"));
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));
    }

    [Fact]
    public async Task A_recorded_change_cannot_be_saved_outside_a_transaction_that_holds_both()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        await world.Factory.ForClinicAsync(world.Clinic, services =>
        {
            var db = services.GetRequiredService<IdentityDbContext>();
            var role = NewRole("بلا معاملة");
            db.Roles.Add(role);
            db.Record(Created(role));

            // The save that cannot wait for the entry is refused outright.
            Assert.Throws<AuditRefusedException>(() => db.SaveChanges());
            return Task.CompletedTask;
        });

        Assert.Equal(0, await RolesNamedAsync(world, "بلا معاملة"));
        Assert.Empty(await sql.Main.EntriesAsync(world.Clinic));
    }

    [Fact]
    public async Task Recording_with_no_clinic_established_is_refused()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        using var scope = world.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Record(new AuditRecord(AuditActions.SignInSucceeded, AuditTarget.ForAccount(Guid.NewGuid())));

        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        Assert.Equal(0, await sql.ScalarAsync<int>(sql.Main.Crossing, "SELECT COUNT(*) FROM [audit].Entry WHERE ClinicId = '00000000-0000-0000-0000-000000000000'"));
    }

    [Fact]
    public async Task Who_where_and_when_come_from_the_session_the_request_and_the_clock()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);
        var (colleague, _) = await world.AccountAsync();
        var doctor = await world.Factory.ReadyRoleAsync(world.Clinic, "doctor");

        // The platform's clock, not the database's and not the caller's.
        world.Factory.Clock.Advance(TimeSpan.FromMinutes(4));
        var expected = world.Factory.Clock.GetUtcNow();

        owner.Browser.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0) Chrome/126.0 Safari/537.36");
        using var response = await owner.Browser.PostAsync(new Uri($"/probe/assign/{colleague}/{doctor}", UriKind.Relative), null);
        Assert.True(response.IsSuccessStatusCode, $"Answered {(int)response.StatusCode}.");

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.AccountRoleChanged), e => e.TargetId == colleague);

        Assert.Equal(("staff", owner.AccountId, owner.Email), (entry.ActorKind, entry.ActorAccountId, entry.ActorName));
        Assert.Equal(world.Clinic, entry.ClinicId);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0) Chrome/126.0 Safari/537.36", entry.UserAgent);
        Assert.Matches("^[0-9a-f]{32}$", entry.TraceId);
        Assert.NotEqual(new string('0', 32), entry.TraceId);
        Assert.InRange(entry.OccurredAtUtc, expected.AddSeconds(-1), expected.AddMinutes(1));
    }

    [Fact]
    public async Task What_the_platform_does_alone_names_the_platform_whoever_is_there()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (someone, _) = await world.AccountAsync();

        // Even with a person on the piece of work, the clinic's ready roles are the platform's doing.
        await world.Factory.RolesAsAsync(world.Clinic, someone, roles => roles.ProvisionAsync(default));

        var entries = await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated);
        Assert.Equal(3, entries.Count);
        Assert.All(entries, entry =>
        {
            Assert.Equal("system", entry.ActorKind);
            Assert.Null(entry.ActorAccountId);
            Assert.Null(entry.ActorName);
            Assert.Null(entry.IpAddress);
            Assert.Null(entry.UserAgent);
        });
    }

    [Fact]
    public async Task On_a_request_nothing_can_name_an_actor_who_is_not_signed_in()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        var (someone, _) = await world.AccountAsync();
        using var nobody = world.Factory.Browser();

        // The handler sets the scope a job would use, naming a real account of the clinic.
        using var response = await nobody.PostAsync(new Uri($"/probe/record-as/{world.Clinic}/{someone}", UriKind.Relative), null);
        Assert.True(response.IsSuccessStatusCode, $"Answered {(int)response.StatusCode}.");

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated));
        Assert.Equal(("system", (Guid?)null, (string?)null), (entry.ActorKind, entry.ActorAccountId, entry.ActorName));
    }

    [Fact]
    public async Task On_a_signed_in_request_the_actor_is_the_signed_in_person_whatever_is_named()
    {
        using var world = await RefusalWorld.CreateAsync(sql);
        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);
        var (someone, _) = await world.AccountAsync();

        using var response = await owner.Browser.PostAsync(new Uri($"/probe/record-as/{world.Clinic}/{someone}", UriKind.Relative), null);
        Assert.True(response.IsSuccessStatusCode, $"Answered {(int)response.StatusCode}.");

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated), e => e.TargetName == "باسم غيره");
        Assert.Equal(("staff", owner.AccountId, owner.Email), (entry.ActorKind, entry.ActorAccountId, entry.ActorName));
    }

    [Fact]
    public async Task With_nobody_signed_in_and_nobody_named_the_actor_is_the_platform()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        await world.Factory.RolesAsync(world.Clinic, roles => roles.CreateAsync("بلا فاعل", null, [Permissions.BookingsView], default));

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated));
        Assert.Equal(("system", (Guid?)null), (entry.ActorKind, entry.ActorAccountId));
    }
}
