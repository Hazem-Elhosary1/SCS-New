using Microsoft.Data.SqlClient;
using Scs.Identity.Domain;
using Scs.Notifications.Domain;

namespace Scs.IntegrationTests.Isolation;

/// <summary>Rows for the notifications engine's tables (spec 008), built with raw SQL.</summary>
internal static class NotificationRows
{
    public static async Task<Guid> InsertNotificationAsync(SqlConnection connection, Guid clinicId, Guid? recipient = null)
    {
        var id = Guid.CreateVersion7();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [notify].Notification (Id, ClinicId, RecipientAccountId, EventKey, Mark, Occasion, RaisedAtUtc)
            VALUES (@id, @clinic, @recipient, 'account.new-device', 'warning', '', SYSDATETIMEOFFSET())
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@recipient", recipient ?? Guid.CreateVersion7());
        await command.ExecuteNonQueryAsync();
        return id;
    }
}

public sealed class NotificationSample : IIsolationSample
{
    public Type EntityType => typeof(Notification);

    public string Table => "[notify].Notification";

    public string HarmlessChange => "Link = Link";

    public Task InsertAsync(SqlConnection connection, Guid clinicId) => NotificationRows.InsertNotificationAsync(connection, clinicId);
}

public sealed class OutgoingEmailSample : IIsolationSample
{
    public Type EntityType => typeof(OutgoingEmail);

    public string Table => "[notify].OutgoingEmail";

    public string HarmlessChange => "Attempts = Attempts";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        var notification = await NotificationRows.InsertNotificationAsync(connection, clinicId);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [notify].OutgoingEmail (Id, ClinicId, NotificationId, State, Attempts, NextAttemptAtUtc, CreatedAtUtc)
            VALUES (NEWID(), @clinic, @notification, 'waiting', 0, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@notification", notification);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class EmailSwitchSample : IIsolationSample
{
    public Type EntityType => typeof(EmailSwitch);

    public string Table => "[notify].EmailSwitch";

    public string HarmlessChange => "Enabled = Enabled";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO [notify].EmailSwitch (ClinicId, EventKey, Enabled, ChangedAtUtc) VALUES (@clinic, 'invoice.issued', 0, SYSDATETIMEOFFSET())";
        command.Parameters.AddWithValue("@clinic", clinicId);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class KnownDeviceSample : IIsolationSample
{
    public Type EntityType => typeof(KnownDevice);

    public string Table => "[identity].KnownDevice";

    public string HarmlessChange => "LastSeenAtUtc = LastSeenAtUtc";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        var account = await IdentityRows.InsertAccountAsync(connection, clinicId);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [identity].KnownDevice (Id, ClinicId, AccountId, MarkHash, FirstSeenAtUtc, LastSeenAtUtc)
            VALUES (NEWID(), @clinic, @account, CONVERT(binary(32), HASHBYTES('SHA2_256', CAST(NEWID() AS varchar(36)))),
                    SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        command.Parameters.AddWithValue("@account", account);
        await command.ExecuteNonQueryAsync();
    }
}
