using Microsoft.Data.SqlClient;
using Scs.Subscriptions.Domain;

namespace Scs.IntegrationTests.Isolation;

/// <summary>A clinic's plan link (spec 014), built with raw SQL: one row per clinic, keyed by it.</summary>
public sealed class ClinicPlanSample : IIsolationSample
{
    public Type EntityType => typeof(ClinicPlan);

    public string Table => "[subscriptions].ClinicPlan";

    public string HarmlessChange => "SetAtUtc = SetAtUtc";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO [subscriptions].ClinicPlan (ClinicId, PlanId, SetAtUtc) VALUES (@clinic, NULL, SYSDATETIMEOFFSET())";
        command.Parameters.AddWithValue("@clinic", clinicId);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>One use of a monthly limit (spec 014), built with raw SQL.</summary>
public sealed class MeterEntrySample : IIsolationSample
{
    public Type EntityType => typeof(MeterEntry);

    public string Table => "[subscriptions].MeterEntry";

    public string HarmlessChange => "Purpose = Purpose";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [subscriptions].MeterEntry (Id, ClinicId, FeatureKey, Month, Amount, AtUtc, Purpose, [By], BeyondAllowance)
            VALUES (NEWID(), @clinic, 'message_credits', '2026-10', 1, SYSDATETIMEOFFSET(), N'message.sent', N'job:test', 0)
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        await command.ExecuteNonQueryAsync();
    }
}
