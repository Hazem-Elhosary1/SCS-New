namespace Scs.Platform.Domain;

/// <summary>
/// Proof that background work runs. There is exactly one row, updated by the heartbeat job on
/// every run (specs/001-platform-foundation/data-model.md).
/// </summary>
public sealed class JobHeartbeat
{
    public const short SingleId = 1;
    public const string HeartbeatJobName = "platform-heartbeat";
    public const int JobNameMaxLength = 100;
    public const int VersionMaxLength = 40;

    private JobHeartbeat()
    {
    }

    /// <summary>Always 1: the table holds a single row.</summary>
    public short Id { get; private set; } = SingleId;

    public string JobName { get; private set; } = HeartbeatJobName;

    public DateTimeOffset LastRunAtUtc { get; private set; }

    /// <summary>Version of the release that last ran the job.</summary>
    public string LastRunVersion { get; private set; } = "";

    public void RecordRun(DateTimeOffset at, string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        LastRunAtUtc = at.ToUniversalTime();
        LastRunVersion = version.Length > VersionMaxLength ? version[..VersionMaxLength] : version;
    }
}
