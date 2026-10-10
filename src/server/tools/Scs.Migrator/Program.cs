namespace Scs.Migrator;

internal static class EntryPoint
{
    private static async Task<int> Main(string[] args)
    {
        if (args is ["seed"])
        {
            return await SeedAsync().ConfigureAwait(false);
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        return await Migrator.RunAsync(
            Environment.GetEnvironmentVariable(Migrator.ConnectionVariable),
            Console.Out,
            Console.Error,
            cancellation.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>Scs.Migrator seed</c>: adds the made-up clinics local and test environments start with.
    /// Refuses to run when the environment is production.
    /// </summary>
    private static async Task<int> SeedAsync()
    {
        if (Environment.GetEnvironmentVariable("SCS_ENVIRONMENT") == "production")
        {
            await Console.Error.WriteLineAsync("Seeding is for local and test environments only.").ConfigureAwait(false);
            return 2;
        }

        var connection = Environment.GetEnvironmentVariable("SCS_DB__CROSSING_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            await Console.Error.WriteLineAsync("Missing required configuration value SCS_DB__CROSSING_CONNECTION.").ConfigureAwait(false);
            return 2;
        }

        var added = await Scs.Tenancy.TenancyModule.SeedAsync(connection, CancellationToken.None).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(
            added.Count == 0 ? "Seed clinics are already present." : $"Seeded clinics: {string.Join(", ", added)}.")
            .ConfigureAwait(false);

        var appConnection = Environment.GetEnvironmentVariable("SCS_DB__CONNECTION");
        var staffPassword = Environment.GetEnvironmentVariable("SCS_SEED__STAFF_PASSWORD");
        if (string.IsNullOrWhiteSpace(appConnection) || string.IsNullOrWhiteSpace(staffPassword))
        {
            await Console.Error.WriteLineAsync(
                "Missing required configuration value SCS_DB__CONNECTION or SCS_SEED__STAFF_PASSWORD.").ConfigureAwait(false);
            return 2;
        }

        var clinics = await Scs.Tenancy.TenancyModule.SeededClinicIdsAsync(connection, CancellationToken.None).ConfigureAwait(false);
        var accounts = await Scs.Identity.IdentityModule.SeedAsync(appConnection, clinics, staffPassword, CancellationToken.None)
            .ConfigureAwait(false);
        await Console.Out.WriteLineAsync(
            accounts.Count == 0 ? "Seed staff accounts are already present." : $"Seeded staff accounts: {accounts.Count}.")
            .ConfigureAwait(false);
        return 0;
    }
}
