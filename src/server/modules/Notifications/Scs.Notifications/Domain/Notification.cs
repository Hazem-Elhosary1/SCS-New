using Scs.SharedKernel;

namespace Scs.Notifications.Domain;

/// <summary>
/// One event told to one person (spec 008, FR-016). Written only by the shared writer, on the
/// connection of the change that caused it; this module reads it. It is stored as facts and
/// read as words, and the only change it ever takes is being read by its recipient.
/// </summary>
public sealed class Notification : IClinicOwned
{
    private Notification()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid RecipientAccountId { get; private set; }

    public string EventKey { get; private set; } = "";

    /// <summary>How the bell draws it; copied from the event when it was raised.</summary>
    public string Mark { get; private set; } = "";

    public string? SubjectKind { get; private set; }

    public Guid? SubjectId { get; private set; }

    /// <summary>The identifier a person reads for the record: <c>T-1046</c>.</summary>
    public string? SubjectLabel { get; private set; }

    /// <summary>Empty, or what makes a repeat distinct: a reminder's day-count.</summary>
    public string Occasion { get; private set; } = "";

    /// <summary>A JSON object whose keys are within the event's allowed keys; the facts of the moment.</summary>
    public string? Facts { get; private set; }

    /// <summary>A path inside the console; null when it leads nowhere.</summary>
    public string? Link { get; private set; }

    public DateTimeOffset RaisedAtUtc { get; private set; }

    /// <summary>Null while unread.</summary>
    public DateTimeOffset? ReadAtUtc { get; private set; }

    public override string ToString() => $"{nameof(Notification)}#{Id}";
}

/// <summary>
/// One email owed for one notification (FR-031). It holds no address and no text: both are
/// produced at the moment of sending, from the account as it is then.
/// </summary>
public sealed class OutgoingEmail : IClinicOwned
{
    private OutgoingEmail()
    {
    }

    public Guid Id { get; private set; }

    public Guid ClinicId { get; private set; }

    public Guid NotificationId { get; private set; }

    public Notification? Notification { get; private set; }

    /// <summary><c>waiting</c>, <c>sent</c>, <c>gave-up</c> or <c>dropped</c>.</summary>
    public string State { get; private set; } = "";

    public byte Attempts { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? SettledAtUtc { get; private set; }

    /// <summary>It left, or will never leave: sent, given up, or dropped.</summary>
    public void Settle(string state, DateTimeOffset now)
    {
        State = state;
        SettledAtUtc = now;
    }

    /// <summary>The mail server did not take it; try again later.</summary>
    public void Postpone(DateTimeOffset until)
    {
        Attempts = (byte)Math.Min(byte.MaxValue, Attempts + 1);
        NextAttemptAtUtc = until;
    }

    public override string ToString() => $"{nameof(OutgoingEmail)}#{Id}";
}

/// <summary>
/// A clinic's choice for the email of one switchable event (FR-037). A row exists only once the
/// switch was changed; with no row the email is on.
/// </summary>
public sealed class EmailSwitch : IClinicOwned
{
    private EmailSwitch()
    {
    }

    public EmailSwitch(string eventKey, bool enabled, Guid? by, DateTimeOffset now)
    {
        EventKey = eventKey;
        Set(enabled, by, now);
    }

    public Guid ClinicId { get; private set; }

    public string EventKey { get; private set; } = "";

    public bool Enabled { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public Guid? ChangedByAccountId { get; private set; }

    public void Set(bool enabled, Guid? by, DateTimeOffset now)
    {
        Enabled = enabled;
        ChangedByAccountId = by;
        ChangedAtUtc = now;
    }

    public override string ToString() => $"{nameof(EmailSwitch)}#{EventKey}";
}
