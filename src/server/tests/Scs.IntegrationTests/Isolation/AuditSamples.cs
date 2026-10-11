using Microsoft.Data.SqlClient;
using Scs.Audit.Domain;

namespace Scs.IntegrationTests.Isolation;

/// <summary>Isolation sample for the audit trail's one table (spec 007).</summary>
public sealed class AuditEntrySample : IIsolationSample
{
    public Type EntityType => typeof(Entry);

    public string Table => "[audit].Entry";

    public string HarmlessChange => "TargetName = TargetName";

    public Task InsertAsync(SqlConnection connection, Guid clinicId) => InsertAsync(connection, clinicId, "staff");

    public static async Task<Guid> InsertAsync(SqlConnection connection, Guid clinicId, string actorKind)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var id = Guid.CreateVersion7();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [audit].Entry (Id, ClinicId, OccurredAtUtc, ActorKind, ActorAccountId, ActorName, ActionCode, Category, TraceId)
            VALUES (@id, @clinic, SYSDATETIMEOFFSET(), @kind, @actor, @name, 'role.created', 'users', REPLICATE('0', 32))
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@kind", actorKind);
        command.Parameters.AddWithValue("@actor", actorKind == "system" ? DBNull.Value : Guid.CreateVersion7());
        command.Parameters.AddWithValue("@name", actorKind == "system" ? DBNull.Value : "someone@isolation.example");
        await command.ExecuteNonQueryAsync();
        return id;
    }
}
