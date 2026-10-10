using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Scs.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace Scs.IntegrationTests.Isolation;

/// <summary>
/// Spec 002, SC-009 and SC-010: with 200 clinics and work running for 50 of them at the same
/// moment, no piece of work ever receives another clinic's row; and establishing the clinic
/// adds no delay a user would notice.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class IsolationUnderLoadTests(SqlServerFixture sql, ITestOutputHelper output)
{
    private const int Clinics = 200;
    private const int Concurrent = 50;
    private const int Rounds = 8;
    private const int NotesPerClinic = 3;

    [Fact]
    public async Task Fifty_clinics_working_at_once_never_see_each_others_rows()
    {
        var database = await PredicateBehaviourTests.CreateProbeDatabaseAsync(sql);
        var clinics = Enumerable.Range(0, Clinics).Select(_ => Guid.CreateVersion7()).ToArray();

        await using (var seed = await TestDatabase.OpenAsync(database.Crossing))
        {
            foreach (var chunk in clinics.Chunk(50))
            {
                var rows = string.Join(",", chunk.SelectMany(clinic =>
                    Enumerable.Range(0, NotesPerClinic).Select(n => $"(NEWID(), '{clinic}', N'{clinic}:{n}')")));
                await TestDatabase.ExecuteAsync(seed, $"INSERT INTO probe.Note (Id, ClinicId, Body) VALUES {rows}");
            }
        }

        // A small pool, so connections are handed from one clinic's work to another's all the time.
        var pooled = new SqlConnectionStringBuilder(database.App)
        {
            MaxPoolSize = 12,
            ApplicationName = $"load-{Guid.NewGuid():N}",
        }.ConnectionString;

        var foreign = 0;
        var checkedRows = 0;
        var random = new Random(20261010);

        for (var round = 0; round < Rounds; round++)
        {
            var working = clinics.OrderBy(_ => random.Next()).Take(Concurrent).ToArray();

            await Task.WhenAll(working.Select(async clinic =>
            {
                await using var connection = await TestDatabase.OpenAsync(pooled, clinic);

                // Read everything visible, then try to touch everything.
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT ClinicId, Body FROM probe.Note";
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    var seen = 0;
                    while (await reader.ReadAsync())
                    {
                        seen++;
                        if (reader.GetGuid(0) != clinic || !reader.GetString(1).StartsWith(clinic.ToString(), StringComparison.Ordinal))
                        {
                            Interlocked.Increment(ref foreign);
                        }
                    }

                    Interlocked.Add(ref checkedRows, seen);
                    Assert.Equal(NotesPerClinic, seen);
                }

                var touched = await TestDatabase.ExecuteAsync(connection, "UPDATE probe.Note SET Body = Body");
                Assert.Equal(NotesPerClinic, touched);
            }));
        }

        Assert.Equal(0, foreign);
        Assert.Equal(Rounds * Concurrent * NotesPerClinic, checkedRows);

        // Nothing was moved or lost along the way.
        Assert.Equal(Clinics * NotesPerClinic, await TestDatabase.ScalarAsync<int>(database.Crossing, "SELECT COUNT(*) FROM probe.Note"));
        Assert.Equal(Clinics, await TestDatabase.ScalarAsync<int>(database.Crossing, "SELECT COUNT(DISTINCT ClinicId) FROM probe.Note"));
        Assert.Equal(0, await TestDatabase.ScalarAsync<int>(pooled, "SELECT COUNT(*) FROM probe.Note"));
    }

    [Fact]
    public async Task Establishing_the_clinic_adds_no_delay_a_user_would_notice()
    {
        var database = await PredicateBehaviourTests.CreateProbeDatabaseAsync(sql);
        var clinic = Guid.CreateVersion7();
        var pooled = new SqlConnectionStringBuilder(database.App) { ApplicationName = $"timing-{Guid.NewGuid():N}" }.ConnectionString;
        const int iterations = 300;

        // Warm the pool and the plan cache.
        for (var i = 0; i < 20; i++)
        {
            await using var warm = await TestDatabase.OpenAsync(pooled, clinic);
            await TestDatabase.ScalarAsync<int>(warm, "SELECT COUNT(*) FROM probe.Note");
        }

        var without = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            await using var connection = await TestDatabase.OpenAsync(pooled);
            await TestDatabase.ScalarAsync<int>(connection, "SELECT COUNT(*) FROM probe.Note");
        }

        without.Stop();

        var with = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            await using var connection = await TestDatabase.OpenAsync(pooled, clinic);
            await TestDatabase.ScalarAsync<int>(connection, "SELECT COUNT(*) FROM probe.Note");
        }

        with.Stop();

        var addedMs = (with.Elapsed - without.Elapsed).TotalMilliseconds / iterations;
        output.WriteLine($"Per request: {without.Elapsed.TotalMilliseconds / iterations:F2} ms without a clinic, " +
                         $"{with.Elapsed.TotalMilliseconds / iterations:F2} ms with; added {addedMs:F2} ms.");

        // The goal is 5 ms (plan.md); the assertion leaves room for a busy build machine while
        // still failing if setting the clinic ever became a visible cost (SC-010: a tenth of a second).
        Assert.True(addedMs < 25, $"Establishing the clinic added {addedMs:F2} ms per request.");
    }
}
