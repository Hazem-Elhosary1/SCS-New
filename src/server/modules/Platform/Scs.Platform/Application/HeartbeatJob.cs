using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Security;
using Scs.Platform.Domain;

namespace Scs.Platform.Application;

/// <summary>Where the heartbeat row is kept; implemented by the persistence layer.</summary>
public interface IHeartbeatStore
{
    Task<JobHeartbeat> GetAsync(CancellationToken cancellation);

    Task SaveAsync(CancellationToken cancellation);
}

/// <summary>
/// The recurring job <c>platform-heartbeat</c>. Running it twice has the same result as once:
/// it only stamps the single row with the time and the running version.
/// </summary>
[RecordsNothing("stamps one row to prove background work runs; nothing a person would trace")]
public sealed class HeartbeatJob(IHeartbeatStore store, IOptions<ScsOptions> options, TimeProvider clock)
{
    public const string Cron = "* * * * *";

    public async Task RunAsync(CancellationToken cancellation)
    {
        var heartbeat = await store.GetAsync(cancellation).ConfigureAwait(false);
        heartbeat.RecordRun(clock.GetUtcNow(), options.Value.Version);
        await store.SaveAsync(cancellation).ConfigureAwait(false);
    }
}
