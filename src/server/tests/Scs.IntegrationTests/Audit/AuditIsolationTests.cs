using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scs.Audit.Infrastructure.Persistence;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.IntegrationTests.Isolation;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.IntegrationTests.Audit;

/// <summary>
/// Spec 007, FR-005 and constitution II: an entry belongs to one clinic, and no way of reading
/// reaches it from another. Written before the table existed.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class AuditIsolationTests(SqlServerFixture sql)
{
    /// <summary>Two clinics; a person of the first has created a role, which is on its trail.</summary>
    private async Task<(RefusalWorld World, Guid A, Guid B, Guid Actor, Guid EntryId)> TwoClinicsAsync()
    {
        var world = await RefusalWorld.CreateAsync(sql);
        var other = await sql.Main.SeedClinicAsync();
        var (actor, _) = await world.AccountAsync();

        await world.Factory.RolesAsAsync(
            world.Clinic, actor, roles => roles.CreateAsync("الاستقبال", null, [Permissions.BookingsView], default));

        var entry = Assert.Single(await sql.Main.EntriesAsync(world.Clinic, AuditActions.RoleCreated));
        return (world, world.Clinic, other, actor, entry.Id);
    }

    [Fact]
    public async Task The_table_is_isolated_like_every_clinic_owned_table()
    {
        await sql.EnsureMigratedAsync();

        var violations = await IsolationCatalog.FindViolationsAsync(sql.AdminConnection);

        Assert.DoesNotContain(violations, line => line.StartsWith("audit.", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            5,
            await sql.ScalarAsync<int>(
                sql.AdminConnection,
                "SELECT COUNT(*) FROM sys.security_predicates WHERE target_object_id = OBJECT_ID(N'audit.Entry')"));
    }

    [Fact]
    public async Task One_clinics_entry_is_not_read_by_another_through_the_reading()
    {
        var (world, a, b, actor, entryId) = await TwoClinicsAsync();
        using var _ = world;

        var own = await world.Factory.ReadTrailAsync(a);
        var others = await world.Factory.ReadTrailAsync(b);

        Assert.Contains(own.Entries, entry => entry.Id == entryId);
        Assert.Empty(others.Entries);
        Assert.Equal(0, others.Total);
        Assert.Equal(0, others.Counts.All);

        // Asking for the other clinic's person by name or by id changes nothing.
        var asked = await world.Factory.ReadTrailAsync(b, Trail.Everything() with { Actor = actor });
        Assert.Empty(asked.Entries);
    }

    [Fact]
    public async Task One_clinics_entry_is_not_found_by_its_id_from_another()
    {
        var (world, a, b, _, entryId) = await TwoClinicsAsync();
        using var _ = world;

        var fromOwn = await world.Factory.ForClinicAsync(a, services =>
            services.GetRequiredService<AuditDbContext>().Entries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == entryId));
        var fromOther = await world.Factory.ForClinicAsync(b, services =>
            services.GetRequiredService<AuditDbContext>().Entries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == entryId));

        // The application's own filter set aside, the database still answers nothing.
        var pastTheFilter = await world.Factory.ForClinicAsync(b, services =>
            services.GetRequiredService<AuditDbContext>().Entries.IgnoreQueryFilters().AsNoTracking().CountAsync(e => e.Id == entryId));

        Assert.NotNull(fromOwn);
        Assert.Null(fromOther);
        Assert.Equal(0, pastTheFilter);
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(sql.Main.App, $"SELECT COUNT(*) FROM [audit].Entry WHERE Id = '{entryId}'", b));
    }

    [Fact]
    public async Task With_no_clinic_established_nothing_is_read()
    {
        var (world, _, _, _, _) = await TwoClinicsAsync();
        using var __ = world;

        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(sql.Main.App, "SELECT COUNT(*) FROM [audit].Entry"));

        using var scope = world.Factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Entries.CountAsync());
    }

    [Fact]
    public async Task An_entry_cannot_be_written_for_another_clinic()
    {
        var (world, a, b, _, _) = await TwoClinicsAsync();
        using var _ = world;
        var before = (await sql.Main.EntriesAsync(b)).Count;

        await using (var asA = await TestDatabase.OpenAsync(sql.Main.App, a))
        {
            await Assert.ThrowsAsync<SqlException>(() => AuditEntrySample.InsertAsync(asA, b, "staff"));
        }

        await using (var asNobody = await TestDatabase.OpenAsync(sql.Main.App))
        {
            await Assert.ThrowsAsync<SqlException>(() => AuditEntrySample.InsertAsync(asNobody, b, "staff"));
        }

        Assert.Equal(before, (await sql.Main.EntriesAsync(b)).Count);
    }

    [Fact]
    public async Task What_one_clinic_does_lands_in_its_own_trail_only()
    {
        var (world, a, b, _, _) = await TwoClinicsAsync();
        using var _ = world;

        Assert.All(await sql.Main.EntriesAsync(a), entry => Assert.Equal(a, entry.ClinicId));
        Assert.Empty(await sql.Main.EntriesAsync(b));
    }
}
