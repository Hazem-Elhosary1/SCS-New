using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.Audit.Infrastructure.Persistence;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.IntegrationTests.Audit;

/// <summary>
/// Spec 007, User Story 2 (FR-003, FR-020 to FR-022): an entry is never changed and never
/// removed by the application, and it goes on saying what was true when it was written.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuditImmutabilityTests(SqlServerFixture sql)
{
    private async Task<(RefusalWorld World, Guid Actor, string ActorEmail, Guid RoleId, StoredEntry Entry)> WithAnEntryAsync()
    {
        var world = await RefusalWorld.CreateAsync(sql);
        var (actor, email) = await world.AccountAsync();
        var role = await world.Factory.RolesAsAsync(
            world.Clinic, actor, roles => roles.CreateAsync("الاستقبال", "مكتب المواعيد", [Permissions.BookingsView], default));

        return (world, actor, email, role, Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated)));
    }

    [Fact]
    public async Task The_application_cannot_change_an_entry()
    {
        var (world, _, _, _, entry) = await WithAnEntryAsync();
        using var _ = world;

        var refusal = await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(
            sql.Main.App, $"UPDATE [audit].Entry SET TargetName = N'غيره' WHERE Id = '{entry.Id}'", world.Clinic));

        Assert.Equal(51007, refusal.Number);
        Assert.Equal(entry, Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated)));
    }

    [Fact]
    public async Task The_application_cannot_remove_an_entry()
    {
        var (world, _, _, _, entry) = await WithAnEntryAsync();
        using var _ = world;

        var refusal = await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(
            sql.Main.App, $"DELETE FROM [audit].Entry WHERE Id = '{entry.Id}'", world.Clinic));

        Assert.Equal(51007, refusal.Number);
        Assert.Equal(entry, Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated)));
    }

    [Fact]
    public async Task Nobody_changes_an_entry_not_even_across_clinics()
    {
        var (world, _, _, _, entry) = await WithAnEntryAsync();
        using var _ = world;

        await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(
            sql.Main.Crossing, $"UPDATE [audit].Entry SET ActorName = N'someone else' WHERE Id = '{entry.Id}'"));

        Assert.Equal(entry, Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated)));
    }

    [Fact]
    public async Task Nobody_removes_an_entry_not_the_operator_connection_and_not_the_migrator()
    {
        var (world, _, _, _, entry) = await WithAnEntryAsync();
        using var _ = world;

        // The application holds the operator connection: what it could do, the application could.
        foreach (var login in new[] { sql.Main.Crossing, sql.Main.Migration })
        {
            var removal = await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(
                login, $"DELETE FROM [audit].Entry WHERE Id = '{entry.Id}'"));
            var change = await Assert.ThrowsAsync<SqlException>(() => TestDatabase.ExecuteAsync(
                login, $"UPDATE [audit].Entry SET TargetName = N'غيره' WHERE Id = '{entry.Id}'"));

            Assert.Equal((51007, 51007), (removal.Number, change.Number));
        }

        // The administrator is in no clinic and not of the cross-clinic role: isolation shows it no
        // entry at all, so there is nothing for it to remove or change.
        Assert.Equal(0, await TestDatabase.ExecuteAsync(sql.Main.Admin, $"DELETE FROM [audit].Entry WHERE Id = '{entry.Id}'"));
        Assert.Equal(0, await TestDatabase.ExecuteAsync(sql.Main.Admin, $"UPDATE [audit].Entry SET TargetName = N'x' WHERE Id = '{entry.Id}'"));

        Assert.Equal(entry, Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated)));
    }

    [Fact]
    public async Task The_modules_own_context_cannot_change_or_remove_one_either()
    {
        var (world, _, _, _, entry) = await WithAnEntryAsync();
        using var _ = world;

        await world.Factory.ForClinicAsync(world.Clinic, async services =>
        {
            var db = services.GetRequiredService<AuditDbContext>();
            var tracked = await db.Entries.SingleAsync(e => e.Id == entry.Id);

            db.Entry(tracked).Property(e => e.TargetName).CurrentValue = "غيره";
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

            db.ChangeTracker.Clear();
            db.Entries.Remove(await db.Entries.SingleAsync(e => e.Id == entry.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<SqlException>(() => db.Entries.Where(e => e.Id == entry.Id).ExecuteDeleteAsync());
        });

        Assert.Equal(entry, Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated)));
    }

    [Fact]
    public async Task An_entry_still_names_a_role_that_was_renamed_and_then_removed()
    {
        var (world, actor, _, role, entry) = await WithAnEntryAsync();
        using var _ = world;

        await world.Factory.RolesAsAsync(
            world.Clinic, actor, roles => roles.UpdateAsync(role, "مكتب الاستقبال", null, [Permissions.BookingsView], default));
        await world.Factory.RolesAsAsync(world.Clinic, actor, roles => roles.RemoveAsync(role, default));

        Assert.Equal(0, await world.ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].Role WHERE Id = '{role}'"));

        // The first entry is exactly as it was written; the later ones say what came after.
        Assert.Equal(entry, Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated)));
        Assert.Equal("الاستقبال", entry.TargetName);
        Assert.Equal("مكتب الاستقبال", Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleDeleted)).TargetName);

        var read = await world.Factory.ReadTrailAsync(world.Clinic);
        Assert.Equal("الاستقبال", read.Entries.Single(e => e.Action.Code == AuditActions.RoleCreated).Target!.Name);
        Assert.Equal("مكتب الاستقبال", read.Entries.Single(e => e.Action.Code == AuditActions.RoleDeleted).Target!.Name);
    }

    [Fact]
    public async Task An_entry_still_names_its_actor_after_the_account_is_disabled_and_gone()
    {
        var (world, actor, email, _, entry) = await WithAnEntryAsync();
        using var _ = world;

        await world.Factory.ForClinicAsync(world.Clinic, services =>
            services.GetRequiredService<Scs.Identity.Contracts.IStaffAccounts>()
                .SetStateAsync(actor, Scs.Identity.Contracts.StaffAccountStates.Disabled, default));
        Assert.Equal(email, (await world.Factory.ReadTrailAsync(world.Clinic)).Entries.Single(e => e.Id == entry.Id).Actor.Name);

        // No relationship ties an entry to its account: the account can go, the entry stays.
        await world.ExecuteAsync($"DELETE FROM [identity].StaffAccount WHERE Id = '{actor}'");

        var after = (await world.Factory.ReadTrailAsync(world.Clinic)).Entries.Single(e => e.Id == entry.Id);
        Assert.Equal((true, actor, email), (after.Actor.Known, after.Actor.AccountId, after.Actor.Name));
        Assert.Equal(entry, Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated)));
    }

    [Fact]
    public async Task The_table_declares_no_relationship_to_what_it_names()
    {
        await sql.EnsureMigratedAsync();

        Assert.Equal(
            0,
            await sql.ScalarAsync<int>(
                sql.AdminConnection,
                "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'audit.Entry')"));
        Assert.Equal(
            1,
            await sql.ScalarAsync<int>(
                sql.AdminConnection,
                "SELECT COUNT(*) FROM sys.triggers WHERE parent_id = OBJECT_ID(N'audit.Entry') AND is_disabled = 0"));
    }
}
