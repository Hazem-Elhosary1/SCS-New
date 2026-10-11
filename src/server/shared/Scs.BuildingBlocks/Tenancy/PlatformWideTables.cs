namespace Scs.BuildingBlocks.Tenancy;

/// <summary>
/// The one list of tables that belong to no clinic. Every other table in the database must be
/// clinic-owned and isolated, or the build fails (IsolationGuardrailTests). Adding a line here
/// is a reviewed decision: a platform-wide table is visible to every clinic's work and must
/// never hold patient data.
/// </summary>
public static class PlatformWideTables
{
    /// <summary>
    /// Patterns are <c>schema.table</c>; <c>*</c> stands for any schema or any table.
    /// </summary>
    public static IReadOnlyList<(string Pattern, string Reason)> All { get; } =
    [
        ("*.__EFMigrationsHistory", "Which structure changes have been applied, per module."),
        ("hangfire.*", "Background job storage: job types, arguments and schedules, no clinic data."),
        ("platform.JobHeartbeat", "Proof that background work runs; one row for the whole platform."),
        ("tenancy.ClinicDirectory", "Clinic short names, which already appear in public addresses."),
        ("crossing.CrossClinicAccess", "Who used a cross-clinic operation; identifiers only, hidden from clinics."),
        ("platform.DataProtectionKey", "The key ring that protects session cookies; one for the whole platform."),
        ("files.RetentionPeriod", "How long each class of file is kept (SH-095): four classes and a number of days; no clinic data."),
        ("security.AnonymousSignInAttempt", "Sign-in attempts on emails that match no account; a keyed hash, never the email; insert-only for the application."),
    ];

    public static bool Contains(string schema, string table) =>
        All.Any(entry => Matches(entry.Pattern, schema, table));

    private static bool Matches(string pattern, string schema, string table)
    {
        var parts = pattern.Split('.', 2);
        return Part(parts[0], schema) && Part(parts[1], table);

        static bool Part(string expected, string actual) =>
            expected == "*" || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }
}
