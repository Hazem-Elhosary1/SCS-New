namespace Scs.Migrator;

internal static class EntryPoint
{
    private static async Task<int> Main()
    {
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
}
