using Microsoft.Data.SqlClient;
using Scs.ClinicData.Domain;

namespace Scs.IntegrationTests.Isolation;

/// <summary>
/// A clinic's branch (spec 015), built with raw SQL. Not the main one, so that a second row for a
/// clinic is refused by isolation and never by the one-main index.
/// </summary>
public sealed class BranchSample : IIsolationSample
{
    public Type EntityType => typeof(Branch);

    public string Table => "[clinicdata].Branch";

    public string HarmlessChange => "Address = Address";

    public async Task InsertAsync(SqlConnection connection, Guid clinicId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [clinicdata].Branch (Id, ClinicId, NameAr, NameEn, Address, IsMain, CreatedAtUtc)
            VALUES (NEWID(), @clinic, N'فرع', N'Branch', N'العنوان', 0, SYSDATETIMEOFFSET())
            """;
        command.Parameters.AddWithValue("@clinic", clinicId);
        await command.ExecuteNonQueryAsync();
    }
}
