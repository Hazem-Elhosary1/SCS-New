using Scs.SharedKernel;

namespace Scs.Audit.Domain;

/// <summary>
/// One recorded action on the platform itself (spec 011; spec 007 research R5): an operator
/// inviting a colleague, changing a role, and the like. It belongs to no clinic, and no
/// clinic's reading ever returns it. Written only by the trail's writer, on the connection of
/// the change it belongs to; the database refuses to change or remove a row.
/// </summary>
public sealed class PlatformEntry
{
    private PlatformEntry()
    {
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary><c>operator</c>, or <c>system</c> for the deployment tool and the platform's own work.</summary>
    public string ActorKind { get; private set; } = "";

    public Guid? ActorAccountId { get; private set; }

    [PersonalData]
    public string? ActorName { get; private set; }

    public string ActionCode { get; private set; } = "";

    public string Category { get; private set; } = "";

    public string? TargetKind { get; private set; }

    public Guid? TargetId { get; private set; }

    public string? TargetName { get; private set; }

    public string? Facts { get; private set; }

    public string? Changes { get; private set; }

    [PersonalData]
    public string? IpAddress { get; private set; }

    [PersonalData]
    public string? UserAgent { get; private set; }

    public string TraceId { get; private set; } = "";

    public override string ToString() => $"{nameof(PlatformEntry)}#{Id}";
}
