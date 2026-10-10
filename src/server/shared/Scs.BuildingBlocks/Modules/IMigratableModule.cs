namespace Scs.BuildingBlocks.Modules;

/// <summary>
/// A module that owns database structure. The migrator calls this with a login that may alter
/// structure; the application itself never does (research R16).
/// </summary>
public interface IMigratableModule
{
    string Name { get; }

    Task MigrateAsync(string migrationConnection, CancellationToken cancellation);
}
