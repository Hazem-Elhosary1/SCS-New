using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Scs.Files.Infrastructure;

/// <summary>
/// What the last sweep found waiting too long for its scan (spec 009, FR-019). Kept in memory:
/// the sweep runs every minute and writes it again, so a restart loses a minute at most.
/// </summary>
internal sealed class ScanBacklog
{
    private int _files;
    private int _clinics;

    public (int Files, int Clinics) Last => (Volatile.Read(ref _files), Volatile.Read(ref _clinics));

    public void Found(int files, int clinics)
    {
        Volatile.Write(ref _files, files);
        Volatile.Write(ref _clinics, clinics);
    }
}

/// <summary>
/// Reads "degraded" while files have waited more than an hour for their malware scan: the
/// scanner may be down (FR-019). It is not one of the checks of readiness — files waiting are
/// no reason to take the platform out of service — and it holds counts, never a file's name.
/// The alarm on it is wired with the monitoring of spec 001.
/// </summary>
internal sealed class ScanBacklogHealthCheck(ScanBacklog backlog) : IHealthCheck
{
    public const string Name = "files-scan-backlog";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var (files, clinics) = backlog.Last;
        var data = new Dictionary<string, object>(StringComparer.Ordinal) { ["files"] = files, ["clinics"] = clinics };

        return Task.FromResult(files == 0
            ? HealthCheckResult.Healthy("No file has waited more than an hour for its scan.", data)
            : HealthCheckResult.Degraded("Files have waited more than an hour for their malware scan.", data: data));
    }
}
