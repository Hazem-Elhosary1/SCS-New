using Microsoft.Data.SqlClient;
using Scs.BuildingBlocks.Tenancy;

namespace Scs.IntegrationTests.Isolation;

/// <summary>
/// Reads the database's own catalog and reports every table that is neither isolated nor
/// declared platform-wide. The catalog is the only complete list of tables, so nothing has to
/// be remembered: a table that exists and is not protected is found.
/// </summary>
public static class IsolationCatalog
{
    private static readonly string[] RequiredBlocks = ["AFTER INSERT", "AFTER UPDATE", "BEFORE UPDATE", "BEFORE DELETE"];

    private const string Query =
        """
        SELECT s.name, t.name,
               c.is_nullable, TYPE_NAME(c.system_type_id),
               p.is_enabled, p.is_schema_bound,
               pr.predicate_type_desc, pr.operation_desc, pr.predicate_definition
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.name = N'ClinicId'
        LEFT JOIN sys.security_predicates pr ON pr.target_object_id = t.object_id
        LEFT JOIN sys.security_policies p ON p.object_id = pr.object_id
        WHERE t.is_ms_shipped = 0
        ORDER BY s.name, t.name;
        """;

    /// <summary>
    /// Every procedure, function or trigger that runs as someone other than its caller. Such a
    /// module can see what its caller cannot, so each one must be declared (spec 003, FR-056).
    /// </summary>
    private const string ModulesQuery =
        """
        SELECT SCHEMA_NAME(o.schema_id), o.name,
               CASE m.execute_as_principal_id WHEN -2 THEN N'OWNER' ELSE USER_NAME(m.execute_as_principal_id) END
        FROM sys.sql_modules m
        JOIN sys.objects o ON o.object_id = m.object_id
        WHERE m.execute_as_principal_id IS NOT NULL AND o.is_ms_shipped = 0
        ORDER BY 1, 2;
        """;

    /// <summary>One line per problem, each naming the table. Empty when every table is accounted for.</summary>
    public static async Task<IReadOnlyList<string>> FindViolationsAsync(string adminConnection)
    {
        var tables = new Dictionary<string, TableFacts>(StringComparer.OrdinalIgnoreCase);

        await using (var connection = new SqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = Query;
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                var table = reader.GetString(1);
                if (!tables.TryGetValue($"{schema}.{table}", out var facts))
                {
                    facts = new TableFacts(schema, table)
                    {
                        HasColumn = !reader.IsDBNull(2),
                        ColumnNullable = !reader.IsDBNull(2) && reader.GetBoolean(2),
                        ColumnType = reader.IsDBNull(3) ? null : reader.GetString(3),
                    };
                    tables.Add($"{schema}.{table}", facts);
                }

                if (!reader.IsDBNull(6))
                {
                    facts.Predicates.Add(new Predicate(
                        Enabled: reader.GetBoolean(4),
                        SchemaBound: reader.GetBoolean(5),
                        Type: reader.GetString(6),
                        Operation: reader.IsDBNull(7) ? null : reader.GetString(7),
                        Definition: reader.IsDBNull(8) ? "" : reader.GetString(8)));
                }
            }
        }

        var violations = new List<string>();
        foreach (var facts in tables.Values)
        {
            if (PlatformWideTables.Contains(facts.Schema, facts.Table))
            {
                continue;
            }

            violations.AddRange(Check(facts).Select(problem => $"{facts.Schema}.{facts.Table}: {problem}"));
        }

        await using (var connection = new SqlConnection(adminConnection))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = ModulesQuery;
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var name = $"{reader.GetString(0)}.{reader.GetString(1)}";
                var runsAs = reader.IsDBNull(2) ? "an unknown user" : reader.GetString(2);
                if (!DeclaredDatabaseCrossings.Contains(name, runsAs))
                {
                    violations.Add($"{name}: runs as {runsAs} and is not declared in DeclaredDatabaseCrossings");
                }
            }
        }

        return violations;
    }

    private static IEnumerable<string> Check(TableFacts facts)
    {
        if (!facts.HasColumn)
        {
            yield return "has no ClinicId column and is not declared in PlatformWideTables";
            yield break;
        }

        if (facts.ColumnType != "uniqueidentifier")
        {
            yield return $"ClinicId is {facts.ColumnType}, not uniqueidentifier";
        }

        if (facts.ColumnNullable)
        {
            yield return "ClinicId is nullable";
        }

        if (facts.Predicates.Count == 0)
        {
            yield return "has no isolation policy";
            yield break;
        }

        if (facts.Predicates.Any(p => !p.Enabled))
        {
            yield return "its isolation policy is switched off";
        }

        if (facts.Predicates.Any(p => !p.SchemaBound))
        {
            yield return "its isolation policy is not schema-bound";
        }

        var ours = facts.Predicates
            .Where(p => p.Enabled
                        && p.Definition.Contains(ClinicIsolationSql.Function, StringComparison.OrdinalIgnoreCase)
                        && p.Definition.Contains(ClinicIsolationSql.Column, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!ours.Any(p => p.Type == "FILTER"))
        {
            yield return "has no filter predicate on ClinicId";
        }

        foreach (var operation in RequiredBlocks)
        {
            if (!ours.Any(p => p.Type == "BLOCK" && string.Equals(p.Operation, operation, StringComparison.OrdinalIgnoreCase)))
            {
                yield return $"has no {operation} block predicate on ClinicId";
            }
        }
    }

    private sealed class TableFacts(string schema, string table)
    {
        public string Schema { get; } = schema;

        public string Table { get; } = table;

        public bool HasColumn { get; init; }

        public bool ColumnNullable { get; init; }

        public string? ColumnType { get; init; }

        public List<Predicate> Predicates { get; } = [];
    }

    private sealed record Predicate(bool Enabled, bool SchemaBound, string Type, string? Operation, string Definition);
}
