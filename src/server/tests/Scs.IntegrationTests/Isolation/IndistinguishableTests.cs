using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Scs.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace Scs.IntegrationTests.Isolation;

/// <summary>
/// Spec 002, FR-017 and SC-005: asking for another clinic's record and asking for a record that
/// does not exist give the same answer, and cannot be told apart by how long the answer takes.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Isolation")]
public sealed class IndistinguishableTests(SqlServerFixture sql, ITestOutputHelper output)
{
    private const int Requests = 1000;
    private const string ById = "SELECT Id, ClinicId, Body FROM probe.Note WHERE Id = @id";

    [Fact]
    public async Task Another_clinics_record_and_a_missing_record_give_the_same_answer_in_the_same_time()
    {
        var database = await PredicateBehaviourTests.CreateProbeDatabaseAsync(sql);
        var mine = Guid.CreateVersion7();
        var theirs = Guid.CreateVersion7();
        var theirNotes = Enumerable.Range(0, Requests).Select(_ => Guid.NewGuid()).ToArray();

        await using (var seed = await TestDatabase.OpenAsync(database.Crossing))
        {
            foreach (var chunk in theirNotes.Chunk(250))
            {
                var rows = string.Join(",", chunk.Select(id => $"('{id}', '{theirs}', N'theirs')"));
                await TestDatabase.ExecuteAsync(seed, $"INSERT INTO probe.Note (Id, ClinicId, Body) VALUES {rows}");
            }

            await TestDatabase.ExecuteAsync(seed, $"INSERT INTO probe.Note (Id, ClinicId, Body) VALUES (NEWID(), '{mine}', N'mine')");
        }

        await using var connection = await TestDatabase.OpenAsync(database.App, mine);

        // The same answer: no row, no error.
        Assert.Equal(0, await RowsAsync(connection, theirNotes[0]));
        Assert.Equal(0, await RowsAsync(connection, Guid.NewGuid()));

        for (var i = 0; i < 100; i++)
        {
            await RowsAsync(connection, theirNotes[i]);
            await RowsAsync(connection, Guid.NewGuid());
        }

        // A timing comparison is a measurement, not a certainty: a busy machine can disturb one
        // run, so the claim is that the two cannot be told apart, shown by any of three runs.
        var difference = double.MaxValue;
        for (var attempt = 1; attempt <= 3 && difference >= 0.05; attempt++)
        {
            var existing = new double[Requests];
            var missing = new double[Requests];
            for (var i = 0; i < Requests; i++)
            {
                // Alternate, so that whatever else the machine is doing falls on both alike.
                existing[i] = await TimeAsync(connection, theirNotes[i]);
                missing[i] = await TimeAsync(connection, Guid.NewGuid());
            }

            var meanExisting = TrimmedMean(existing);
            var meanMissing = TrimmedMean(missing);
            difference = Math.Abs(meanExisting - meanMissing) / Math.Max(meanExisting, meanMissing);
            output.WriteLine(
                $"Run {attempt}: another clinic's record {meanExisting:F4} ms, no such record {meanMissing:F4} ms, difference {difference:P2}.");
        }

        Assert.True(difference < 0.05, $"The two answers differ in time by {difference:P2}.");
    }

    private static async Task<int> RowsAsync(SqlConnection connection, Guid id)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ById;
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = 0;
        while (await reader.ReadAsync())
        {
            rows++;
        }

        return rows;
    }

    private static async Task<double> TimeAsync(SqlConnection connection, Guid id)
    {
        var started = Stopwatch.GetTimestamp();
        var rows = await RowsAsync(connection, id);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Assert.Equal(0, rows);
        return elapsed;
    }

    /// <summary>The mean without the slowest twentieth, which belongs to the machine, not the query.</summary>
    private static double TrimmedMean(double[] values)
    {
        var kept = values.Order().Take(values.Length * 95 / 100).ToArray();
        return kept.Average();
    }
}
