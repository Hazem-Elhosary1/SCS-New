using Scs.SharedKernel;

namespace Scs.Audit.Domain;

/// <summary>
/// One recorded action (spec 007, FR-001): who, what, on what, in which clinic, the allowed
/// facts, before and after where required, from where and when. Written only by the trail's
/// writer, on the connection of the change it belongs to; this module reads it. The database
/// refuses to change or remove a row.
/// </summary>
public sealed class Entry : IClinicOwned
{
    private Entry()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary><c>staff</c>, <c>operator</c> or <c>system</c>. A failed sign-in is <c>staff</c> with no account.</summary>
    public string ActorKind { get; private set; } = "";

    public Guid? ActorAccountId { get; private set; }

    /// <summary>The display name at the moment: the email address until accounts have names (spec 026).</summary>
    [PersonalData]
    public string? ActorName { get; private set; }

    public string ActionCode { get; private set; } = "";

    public string Category { get; private set; } = "";

    public string? TargetKind { get; private set; }

    public Guid? TargetId { get; private set; }

    public string? TargetName { get; private set; }

    public string? TargetReadyKey { get; private set; }

    /// <summary>A JSON object whose keys are within the action's allowed keys.</summary>
    public string? Facts { get; private set; }

    /// <summary>A JSON list of what changed: before and after, or added and removed.</summary>
    public string? Changes { get; private set; }

    [PersonalData]
    public string? IpAddress { get; private set; }

    [PersonalData]
    public string? UserAgent { get; private set; }

    public string TraceId { get; private set; } = "";

    public override string ToString() => $"{nameof(Entry)}#{Id}";
}
