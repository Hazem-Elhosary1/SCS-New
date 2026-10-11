using System.Diagnostics;
using Scs.IntegrationTests.Fixtures;
using Scs.SharedKernel.Access;
using Xunit.Abstractions;

namespace Scs.IntegrationTests.Audit;

/// <summary>
/// Spec 007, SC-007 and SC-010: a clinic with a million entries over two years still reads a
/// page of its trail, with its counts, in under a second; and recording adds next to nothing to
/// the change it records. In a database of its own, so the million rows burden no other test.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Volume")]
public sealed class AuditVolumeTests(SqlServerFixture sql, ITestOutputHelper output)
{
    private const int Million = 1_000_000;

    /// <summary>A million entries spread evenly over two years: twelve people, six actions.</summary>
    private static string Fill(Guid clinic) =>
        $"""
         WITH Numbers AS (
             SELECT TOP ({Million}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS N
             FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c
         )
         INSERT INTO [audit].Entry WITH (TABLOCK)
             (Id, ClinicId, OccurredAtUtc, ActorKind, ActorAccountId, ActorName, ActionCode, Category,
              TargetKind, TargetId, IpAddress, UserAgent, TraceId)
         SELECT NEWID(), '{clinic}',
                DATEADD(SECOND, -CAST(n.N AS bigint) * 63, SYSDATETIMEOFFSET()),
                CASE WHEN n.N % 50 = 0 THEN 'system' ELSE 'staff' END,
                CASE WHEN n.N % 50 = 0 OR n.N % 7 = 3 THEN NULL
                     ELSE CAST(CONCAT('00000000-0000-0000-0000-0000000000', FORMAT(n.N % 12 + 10, '00')) AS uniqueidentifier) END,
                CASE WHEN n.N % 50 = 0 OR n.N % 7 = 3 THEN NULL ELSE CONCAT('person', n.N % 12, '@volume.example') END,
                a.Code, a.Category, 'account',
                CAST(CONCAT('00000000-0000-0000-0000-0000000000', FORMAT(n.N % 12 + 10, '00')) AS uniqueidentifier),
                '203.0.113.7', N'Mozilla/5.0 (Windows NT 10.0) Chrome/126.0 Safari/537.36',
                REPLACE(CAST(NEWID() AS char(36)), '-', '')
         FROM Numbers n
         JOIN (VALUES
             (0, 'signin.succeeded', 'security'), (1, 'session.signed-out', 'security'), (2, 'signin.succeeded', 'security'),
             (3, 'signin.failed-run', 'security'), (4, 'account.role-changed', 'users'), (5, 'invitation.accepted', 'users'),
             (6, 'password.reset-completed', 'security')
         ) a(Slot, Code, Category) ON a.Slot = n.N % 7;
         """;

    [Fact]
    public async Task A_million_entries_are_read_a_page_at_a_time_in_under_a_second()
    {
        var db = await sql.CreateScratchDatabaseAsync();
        using var migrated = new StringWriter();
        using var error = new StringWriter();
        Assert.True(await Migrator.Migrator.RunAsync(db.Migration, migrated, error, default) == 0, error.ToString());

        var clinic = await db.SeedClinicAsync();
        var other = await db.SeedClinicAsync();

        var filling = Stopwatch.StartNew();
        await using (var connection = await TestDatabase.OpenAsync(db.Crossing))
        await using (var command = connection.CreateCommand())
        {
            command.CommandTimeout = 600;
#pragma warning disable CA2100 // A fixed statement of this test.
            command.CommandText = Fill(clinic);
#pragma warning restore CA2100
            Assert.Equal(Million, await command.ExecuteNonQueryAsync());
        }

        await TestDatabase.ExecuteAsync(db.Admin, "UPDATE STATISTICS [audit].Entry");
        output.WriteLine($"Filled {Million:N0} entries in {filling.Elapsed.TotalSeconds:N1} s.");

        using var factory = new ApiFactory()
            .With("Scs:DB:CONNECTION", db.App)
            .With("Scs:DB:REPORT_CONNECTION", db.Report)
            .With("Scs:DB:CROSSING_CONNECTION", db.Crossing);

        // Once, unmeasured: the first call of a process pays for building the query.
        await factory.ReadTrailAsync(other);

        var firstTimer = Stopwatch.StartNew();
        var first = await factory.ReadTrailAsync(clinic, Trail.Everything(size: 25));
        firstTimer.Stop();

        var lastPage = (int)Math.Ceiling(first.Total / 25d);
        var lastTimer = Stopwatch.StartNew();
        var last = await factory.ReadTrailAsync(clinic, Trail.Everything(size: 25) with { Page = lastPage });
        lastTimer.Stop();

        var narrowedTimer = Stopwatch.StartNew();
        var narrowed = await factory.ReadTrailAsync(
            clinic, Trail.Everything(size: 25) with { Category = "users", Action = "account.role-changed" });
        narrowedTimer.Stop();

        output.WriteLine($"Ninety days hold {first.Total:N0} entries the clinic reads ({first.Counts.Security:N0} security, {first.Counts.Users:N0} users).");
        output.WriteLine($"First page with its counts: {firstTimer.ElapsedMilliseconds} ms.");
        output.WriteLine($"Last page ({lastPage:N0}) with its counts: {lastTimer.ElapsedMilliseconds} ms.");
        output.WriteLine($"A page narrowed by category and action: {narrowedTimer.ElapsedMilliseconds} ms.");

        // Ninety days of two years, less what the platform did.
        Assert.InRange(first.Total, 100_000, 130_000);
        Assert.Equal(25, first.Entries.Count);
        Assert.InRange(last.Entries.Count, 1, 25);
        Assert.Equal(first.Total, first.Counts.All);
        Assert.Equal(first.Counts.Security + first.Counts.Users, first.Counts.All);
        Assert.True(first.Entries[0].OccurredAt >= first.Entries[^1].OccurredAt);
        Assert.True(first.Entries[^1].OccurredAt > last.Entries[0].OccurredAt);
        Assert.All(narrowed.Entries, entry => Assert.Equal("account.role-changed", entry.Action.Code));

        // Nothing of it is the other clinic's to read.
        Assert.Equal(0, (await factory.ReadTrailAsync(other)).Total);

        Assert.True(firstTimer.Elapsed < TimeSpan.FromSeconds(1), $"The first page took {firstTimer.ElapsedMilliseconds} ms.");
        Assert.True(lastTimer.Elapsed < TimeSpan.FromSeconds(1), $"The last page took {lastTimer.ElapsedMilliseconds} ms.");
        Assert.True(narrowedTimer.Elapsed < TimeSpan.FromSeconds(1), $"The narrowed page took {narrowedTimer.ElapsedMilliseconds} ms.");

        // And writing into a trail this size costs what writing into an empty one does.
        await MeasureRecordingAsync(factory, db.Crossing, clinic, "with a million entries");
    }

    [Fact]
    public async Task Recording_adds_less_than_a_tenth_of_a_second_to_a_role_edit()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var clinic = await sql.Main.SeedClinicAsync();

        await MeasureRecordingAsync(factory, sql.Main.Crossing, clinic, "in an ordinary trail");
    }

    /// <summary>
    /// Times role edits that record an entry. The whole edit — its lock, its save and its entry —
    /// is under a tenth of a second, so the entry alone is too (SC-010).
    /// </summary>
    private async Task MeasureRecordingAsync(ApiFactory factory, string crossing, Guid clinic, string where)
    {
        var (actor, _) = await factory.CreateAccountAsync(clinic);
        var role = await factory.RolesAsAsync(clinic, actor, roles => roles.CreateAsync(
            $"volume-{Guid.NewGuid():N}"[..20], null, [Permissions.BookingsView], default));

        string[][] sets = [[Permissions.BookingsView, Permissions.PatientsView], [Permissions.BookingsView]];
        var times = new List<double>();

        for (var i = 0; i < 21; i++)
        {
            var set = sets[i % 2];
            var timer = Stopwatch.StartNew();
            await factory.RolesAsAsync(clinic, actor, roles => roles.UpdateAsync(role, $"volume {i}", null, set, default));
            timer.Stop();

            // The first pays for building the statements.
            if (i > 0)
            {
                times.Add(timer.Elapsed.TotalMilliseconds);
            }
        }

        times.Sort();
        var median = times[times.Count / 2];
        output.WriteLine($"A role edit with its entry, {where}: median {median:N1} ms, slowest {times[^1]:N1} ms over {times.Count} edits.");

        Assert.Equal(
            21,
            await TestDatabase.ScalarAsync<int>(
                crossing, $"SELECT COUNT(*) FROM [audit].Entry WHERE ClinicId = '{clinic}' AND ActionCode = 'role.updated'"));
        Assert.True(median < 100, $"The median edit took {median:N1} ms.");
    }
}
