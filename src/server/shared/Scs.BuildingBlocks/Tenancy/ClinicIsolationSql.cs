using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Scs.BuildingBlocks.Tenancy;

/// <summary>
/// The SQL that makes clinic isolation a property of the database (SH-004, SH-104).
///
/// One function decides whether a row may be touched: its clinic must equal the clinic set on
/// the connection, or the caller must be in the role an administrator grants to the operator
/// and migrator logins. With no clinic set the comparison is unknown, so nothing is visible and
/// nothing can be written: isolation fails closed. A value of any other type or shape is turned
/// into text and then into an identifier, which yields NULL rather than an error.
///
/// Once a release has applied a migration that calls these helpers, the SQL they produce is
/// frozen: change isolation with a new migration, never by editing what is here.
/// </summary>
public static partial class ClinicIsolationSql
{
    public const string Schema = "tenancy";
    public const string Function = "fn_ClinicAccess";
    public const string SessionKey = "ClinicId";
    public const string CrossClinicRole = "scs_cross_clinic";
    public const string Column = "ClinicId";

    public static string CreateAccessFunction() =>
        $"""
         CREATE FUNCTION [{Schema}].[{Function}](@ClinicId uniqueidentifier)
         RETURNS TABLE
         WITH SCHEMABINDING
         AS
         RETURN
             SELECT 1 AS Allowed
             WHERE @ClinicId = TRY_CONVERT(uniqueidentifier, CONVERT(nvarchar(36), SESSION_CONTEXT(N'{SessionKey}')))
                OR IS_ROLEMEMBER(N'{CrossClinicRole}') = 1;
         """;

    public static string DropAccessFunction() => $"DROP FUNCTION [{Schema}].[{Function}];";

    /// <summary>
    /// A filter (what can be seen, and therefore updated or deleted) and four blocks (a row
    /// cannot be created for, moved to, or altered or removed from another clinic).
    /// </summary>
    public static string CreatePolicy(string schema, string table)
    {
        Check(schema);
        Check(table);

        var target = $"[{schema}].[{table}]";
        var predicate = $"[{Schema}].[{Function}]([{Column}])";

        return $"""
                CREATE SECURITY POLICY [{schema}].[{PolicyName(table)}]
                    ADD FILTER PREDICATE {predicate} ON {target},
                    ADD BLOCK PREDICATE {predicate} ON {target} AFTER INSERT,
                    ADD BLOCK PREDICATE {predicate} ON {target} AFTER UPDATE,
                    ADD BLOCK PREDICATE {predicate} ON {target} BEFORE UPDATE,
                    ADD BLOCK PREDICATE {predicate} ON {target} BEFORE DELETE
                    WITH (STATE = ON, SCHEMABINDING = ON);
                """;
    }

    public static string DropPolicy(string schema, string table)
    {
        Check(schema);
        Check(table);
        return $"DROP SECURITY POLICY [{schema}].[{PolicyName(table)}];";
    }

    public static string PolicyName(string table) => $"{table}Isolation";

    private static void Check(string identifier)
    {
        if (!Identifier().IsMatch(identifier))
        {
            throw new ArgumentException($"'{identifier}' is not a plain SQL identifier.", nameof(identifier));
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();
}

/// <summary>What a migration calls. The table and its isolation go in the same migration.</summary>
public static class ClinicIsolationMigrations
{
    /// <summary>Once, in the Tenancy module's first migration.</summary>
    public static void CreateClinicAccessFunction(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(ClinicIsolationSql.CreateAccessFunction());
    }

    public static void DropClinicAccessFunction(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(ClinicIsolationSql.DropAccessFunction());
    }

    /// <summary>Right after <c>CreateTable</c> for every clinic-owned table.</summary>
    public static void CreateClinicIsolation(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(ClinicIsolationSql.CreatePolicy(schema, table));
    }

    /// <summary>Right before <c>DropTable</c>, and in <c>Down</c>.</summary>
    public static void DropClinicIsolation(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(ClinicIsolationSql.DropPolicy(schema, table));
    }
}
