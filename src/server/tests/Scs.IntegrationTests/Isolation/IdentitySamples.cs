using Microsoft.Data.SqlClient;
using Scs.Identity.Domain;

namespace Scs.IntegrationTests.Isolation;

/// <summary>
/// Isolation samples for the Identity module's clinic-owned tables (specs 003 and 004). Each builds
/// one valid row with raw SQL, so the isolation tests exercise the database and nothing else.
/// </summary>
internal static class IdentityRows
{
    /// <summary>Inserts an account for the clinic and returns its id. The email is unique per call.</summary>
    public static async Task<Guid> InsertAccountAsync(
        SqlConnection connection, Guid clinicId, string? email = null, string state = "active")
    {
        var id = Guid.CreateVersion7();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [identity].StaffAccount (Id, ClinicId, Email, PasswordHash, State, CreatedAtUtc)
            VALUES (@id, @clinic, @email, @hash, @state, SYSDATETIMEOFFSET())
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@email", email ?? $"sample-{id:N}@isolation.example");
        command.Parameters.AddWithValue("@hash", state == "invited" ? DBNull.Value : "v1.1.c2FsdA==.aGFzaA==");
        command.Parameters.AddWithValue("@state", state);
        await command.ExecuteNonQueryAsync();
        return id;
    }
}

public sealed class StaffAccountSample : IIsolationSample
{
    public Type EntityType => typeof(StaffAccount);

    public string Table => "[identity].StaffAccount";

    public string HarmlessChange => "FailedAttempts = FailedAttempts";

    public Task InsertAsync(SqlConnection connection, Guid clinicId) =>
        IdentityRows.InsertAccountAsync(connection, clinicId);
}

public sealed class SignInChallengeSample : IIsolationSample
{
    public Type EntityType => typeof(SignInChallenge);

    public string Table => "[identity].SignInChallenge";

    public string HarmlessChange => "Attempts = Attempts";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        var account = await IdentityRows.InsertAccountAsync(connection, clinicId);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [identity].SignInChallenge
                (Id, ClinicId, AccountId, CodeHash, Attempts, CreatedAtUtc, ExpiresAtUtc, ResendAvailableAtUtc, ResendsUsed)
            VALUES
                (NEWID(), @clinic, @account, CONVERT(binary(32), HASHBYTES('SHA2_256', 'x')), 0,
                 SYSDATETIMEOFFSET(), DATEADD(MINUTE, 10, SYSDATETIMEOFFSET()), SYSDATETIMEOFFSET(), 0)
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@account", account);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class SessionSample : IIsolationSample
{
    public Type EntityType => typeof(Session);

    public string Table => "[identity].Session";

    public string HarmlessChange => "LastUsedAtUtc = LastUsedAtUtc";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        var account = await IdentityRows.InsertAccountAsync(connection, clinicId);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [identity].Session
                (Id, ClinicId, AccountId, RenewalHash, CreatedAtUtc, LastUsedAtUtc, ExpiresAtUtc)
            VALUES
                (NEWID(), @clinic, @account, CONVERT(binary(32), HASHBYTES('SHA2_256', 'x')),
                 SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), DATEADD(HOUR, 12, SYSDATETIMEOFFSET()))
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@account", account);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class SignInEventSample : IIsolationSample
{
    public Type EntityType => typeof(SignInEvent);

    public string Table => "[identity].SignInEvent";

    public string HarmlessChange => "Detail = Detail";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        var account = await IdentityRows.InsertAccountAsync(connection, clinicId);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [identity].SignInEvent (Id, ClinicId, AccountId, Kind, Category, TraceId, OccurredAtUtc)
            VALUES (NEWID(), @clinic, @account, 'signed-in', 'activity', REPLICATE('0', 32), SYSDATETIMEOFFSET())
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@account", account);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class PasswordLinkSample : IIsolationSample
{
    public Type EntityType => typeof(PasswordLink);

    public string Table => "[identity].PasswordLink";

    public string HarmlessChange => "ReplacedAtUtc = ReplacedAtUtc";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        var account = await IdentityRows.InsertAccountAsync(connection, clinicId);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [identity].PasswordLink (Id, ClinicId, AccountId, Kind, SecretHash, CreatedAtUtc, ExpiresAtUtc)
            VALUES
                (NEWID(), @clinic, @account, 'reset', CONVERT(binary(32), HASHBYTES('SHA2_256', 'x')),
                 SYSDATETIMEOFFSET(), DATEADD(MINUTE, 30, SYSDATETIMEOFFSET()))
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@account", account);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class AccountEmailRequestSample : IIsolationSample
{
    public Type EntityType => typeof(AccountEmailRequest);

    public string Table => "[identity].AccountEmailRequest";

    public string HarmlessChange => "Outcome = Outcome";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        var account = await IdentityRows.InsertAccountAsync(connection, clinicId);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [identity].AccountEmailRequest (Id, ClinicId, AccountId, Kind, RequestedAtUtc)
            VALUES (NEWID(), @clinic, @account, 'reset-link', SYSDATETIMEOFFSET())
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@account", account);
        await command.ExecuteNonQueryAsync();
    }
}
