using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Modules;
using Scs.Platform;
using Scs.Tenancy;

namespace Scs.Migrator;

/// <summary>
/// Applies every structure change, in order, with the login that is allowed to alter structure.
/// Run by the pipeline before a release starts and by the local start script; never by the
/// application (FR-013).
/// </summary>
public static class Migrator
{
    public const string ConnectionVariable = "SCS_DB__MIGRATION_CONNECTION";

    /// <summary>Modules that own database structure, in the order they are migrated.</summary>
    public static IReadOnlyList<IMigratableModule> Modules { get; } =
    [
        // First: its first migration creates the function every isolation policy calls.
        new TenancyModule(),
        new Scs.Audit.AuditModule(),
        new Scs.Notifications.NotificationsModule(),
        new Scs.Identity.IdentityModule(),
        new PlatformModule(),
    ];

    public static async Task<int> RunAsync(
        string? migrationConnection, TextWriter output, TextWriter error, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (string.IsNullOrWhiteSpace(migrationConnection))
        {
            await error.WriteLineAsync($"Missing required configuration value {ConnectionVariable}.").ConfigureAwait(false);
            return 2;
        }

        try
        {
            await JobScheduling.InstallSchemaAsync(migrationConnection, cancellation).ConfigureAwait(false);
            await output.WriteLineAsync("Job storage is up to date.").ConfigureAwait(false);

            foreach (var module in Modules)
            {
                await module.MigrateAsync(migrationConnection, cancellation).ConfigureAwait(false);
                await output.WriteLineAsync($"Module {module.Name} is up to date.").ConfigureAwait(false);
            }

            return 0;
        }
#pragma warning disable CA1031 // The tool must turn any failure into a non-zero exit code.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // The type only: an exception message may carry the server name or the login.
            await error.WriteLineAsync($"Migration failed: {exception.GetType().Name}.").ConfigureAwait(false);
            return 1;
        }
    }
}
