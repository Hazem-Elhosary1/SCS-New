using Microsoft.Data.SqlClient;
using Scs.Files.Domain;

namespace Scs.IntegrationTests.Isolation;

/// <summary>A row of the files engine's clinic-owned table (spec 009), built with raw SQL.</summary>
public sealed class StoredFileSample : IIsolationSample
{
    public Type EntityType => typeof(StoredFile);

    public string Table => "[files].StoredFile";

    public string HarmlessChange => "ScanTries = ScanTries";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [files].StoredFile (Id, ClinicId, Kind, Name, SizeBytes, Type, UploadedByAccountId, UploadedAtUtc, Scan, ScanTries)
            VALUES (NEWID(), @clinic, 'dev.sample', N'sample.pdf', 10, 'pdf', NEWID(), SYSDATETIMEOFFSET(), 'pending', 0)
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        await command.ExecuteNonQueryAsync();
    }
}
