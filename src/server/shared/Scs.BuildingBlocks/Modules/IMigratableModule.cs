namespace Scs.BuildingBlocks.Modules;

/// <summary>
/// A module that owns database structure. The migrator calls this with a login that may alter
/// structure; the application itself never does (research R16 of spec 001).
/// </summary>
public interface IMigratableModule
{
    string Name { get; }

    /// <summary>The module's structure changes, oldest first.</summary>
    IReadOnlyList<string> GetMigrations(string migrationConnection);

    /// <summary>
    /// Applies the module's structure changes up to and including <paramref name="targetMigration"/>,
    /// or all of them when it is null. Stepping one at a time is how the isolation guardrail
    /// proves that a table and its isolation arrive in the same change.
    /// </summary>
    Task MigrateAsync(string migrationConnection, string? targetMigration, CancellationToken cancellation);
}

public static class MigratableModuleExtensions
{
    public static Task MigrateAsync(
        this IMigratableModule module, string migrationConnection, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(module);
        return module.MigrateAsync(migrationConnection, targetMigration: null, cancellation);
    }
}
