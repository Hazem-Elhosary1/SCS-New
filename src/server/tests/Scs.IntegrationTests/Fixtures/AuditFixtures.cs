using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Scs.Audit.Contracts;
using Scs.BuildingBlocks.Audit;
using Scs.Identity.Contracts;

namespace Scs.IntegrationTests.Fixtures;

/// <summary>One row of <c>audit.Entry</c>, exactly as it is stored.</summary>
public sealed record StoredEntry(
    Guid Id,
    Guid ClinicId,
    DateTimeOffset OccurredAtUtc,
    string ActorKind,
    Guid? ActorAccountId,
    string? ActorName,
    string ActionCode,
    string Category,
    string? TargetKind,
    Guid? TargetId,
    string? TargetName,
    string? TargetReadyKey,
    string? Facts,
    string? Changes,
    string? IpAddress,
    string? UserAgent,
    string TraceId)
{
    public JsonElement FactsJson => JsonDocument.Parse(Facts ?? "{}").RootElement;

    public JsonElement ChangesJson => JsonDocument.Parse(Changes ?? "[]").RootElement;

    /// <summary>The change recorded for one field; fails when there is none.</summary>
    public JsonElement Change(string field) =>
        ChangesJson.EnumerateArray().Single(change => change.GetProperty("field").GetString() == field);

    public bool HasChange(string field) =>
        ChangesJson.EnumerateArray().Any(change => change.GetProperty("field").GetString() == field);
}

/// <summary>Helpers for the audit trail (spec 007): reading what is stored, and acting as a person.</summary>
public static class Trail
{
    /// <summary>
    /// The clinic's entries as stored, oldest first, read with the operator login so that
    /// nothing of the application stands between the test and the table.
    /// </summary>
    public static async Task<List<StoredEntry>> EntriesAsync(this TestDatabase database, Guid clinicId, string? action = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        await using var connection = await TestDatabase.OpenAsync(database.Crossing);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ClinicId, OccurredAtUtc, ActorKind, ActorAccountId, ActorName, ActionCode, Category,
                   TargetKind, TargetId, TargetName, TargetReadyKey, Facts, Changes, IpAddress, UserAgent, TraceId
            FROM [audit].Entry
            WHERE ClinicId = @clinic AND (@action IS NULL OR ActionCode = @action)
            ORDER BY OccurredAtUtc, ActionCode
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@action", (object?)action ?? DBNull.Value);

        var entries = new List<StoredEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new StoredEntry(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetDateTimeOffset(2),
                reader.GetString(3),
                Maybe<Guid?>(reader, 4),
                Maybe<string>(reader, 5),
                reader.GetString(6),
                reader.GetString(7),
                Maybe<string>(reader, 8),
                Maybe<Guid?>(reader, 9),
                Maybe<string>(reader, 10),
                Maybe<string>(reader, 11),
                Maybe<string>(reader, 12),
                Maybe<string>(reader, 13),
                Maybe<string>(reader, 14),
                Maybe<string>(reader, 15),
                reader.GetString(16)));
        }

        return entries;
    }

    /// <summary>
    /// Puts an entry straight into the table, at a moment of the test's choosing: for what the
    /// writer would not let a test say (a time in the past, an operator as the actor).
    /// </summary>
    public static async Task<Guid> PutEntryAsync(
        this TestDatabase database,
        Guid clinicId,
        string action,
        DateTimeOffset at,
        string actorKind = "staff",
        Guid? actor = null,
        string? actorName = null,
        Guid? target = null,
        string? targetKind = null,
        string? targetName = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        var id = Guid.CreateVersion7();
        await using var connection = await TestDatabase.OpenAsync(database.Crossing);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [audit].Entry
                (Id, ClinicId, OccurredAtUtc, ActorKind, ActorAccountId, ActorName, ActionCode, Category,
                 TargetKind, TargetId, TargetName, TraceId)
            VALUES (@id, @clinic, @at, @kind, @actor, @name, @action, @category, @targetKind, @target, @targetName, @trace)
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@at", at);
        command.Parameters.AddWithValue("@kind", actorKind);
        command.Parameters.AddWithValue("@actor", (object?)actor ?? DBNull.Value);
        command.Parameters.AddWithValue("@name", (object?)actorName ?? DBNull.Value);
        command.Parameters.AddWithValue("@action", action);
        command.Parameters.AddWithValue("@category", Scs.SharedKernel.Audit.AuditActions.Find(action)?.Category ?? "records");
        command.Parameters.AddWithValue("@targetKind", (object?)targetKind ?? DBNull.Value);
        command.Parameters.AddWithValue("@target", (object?)target ?? DBNull.Value);
        command.Parameters.AddWithValue("@targetName", (object?)targetName ?? DBNull.Value);
        command.Parameters.AddWithValue("@trace", id.ToString("N"));
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private static T? Maybe<T>(SqlDataReader reader, int column) =>
        reader.IsDBNull(column) ? default : (T)reader.GetValue(column);

    /// <summary>
    /// Runs work for a clinic as one of its people, the way a signed-in request does, without a
    /// request: the trail takes the actor from the scope a job or a test sets.
    /// </summary>
    public static Task<T> AsStaffAsync<T>(
        this ApiFactory factory, Guid clinicId, Guid accountId, Func<IServiceProvider, Task<T>> work) =>
        factory.ForClinicAsync(clinicId, services =>
        {
            services.GetRequiredService<AuditActorScope>().AccountId = accountId;
            return work(services);
        });

    public static Task AsStaffAsync(this ApiFactory factory, Guid clinicId, Guid accountId, Func<IServiceProvider, Task> work) =>
        factory.AsStaffAsync<object?>(clinicId, accountId, async services =>
        {
            await work(services);
            return null;
        });

    /// <summary>Role operations done by one of the clinic's people.</summary>
    public static Task<T> RolesAsAsync<T>(this ApiFactory factory, Guid clinicId, Guid accountId, Func<IClinicRoles, Task<T>> work) =>
        factory.AsStaffAsync(clinicId, accountId, services => work(services.GetRequiredService<IClinicRoles>()));

    public static Task RolesAsAsync(this ApiFactory factory, Guid clinicId, Guid accountId, Func<IClinicRoles, Task> work) =>
        factory.AsStaffAsync(clinicId, accountId, services => work(services.GetRequiredService<IClinicRoles>()));

    public static AuditQuery Everything(string language = "ar", int size = AuditQuery.MaxSize) =>
        new(null, null, null, null, null, null, 1, size, language);

    /// <summary>The clinic's own reading of its trail, through the module's contract.</summary>
    public static Task<AuditPage> ReadTrailAsync(this ApiFactory factory, Guid clinicId, AuditQuery? query = null) =>
        factory.ForClinicAsync(
            clinicId,
            services => services.GetRequiredService<IClinicAuditReading>().ReadAsync(query ?? Everything(), CancellationToken.None));
}
