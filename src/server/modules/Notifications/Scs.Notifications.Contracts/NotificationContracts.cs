namespace Scs.Notifications.Contracts;

/// <summary>One notification, as words in the reader's language (spec 008, FR-019).</summary>
/// <param name="Kind">The mark the bell draws it with.</param>
/// <param name="Text">The line under the title: the record's identifier and one fact.</param>
/// <param name="Link">A path inside the console; null when it leads nowhere.</param>
public sealed record NotificationView(
    Guid Id, string Kind, string Event, string Title, string Text, DateTimeOffset At, bool Read, string? Link);

/// <param name="Unread">The person has at least one unread notification of the last ninety days.</param>
/// <param name="Next">What to ask the next, older page with; null when there is nothing older.</param>
public sealed record NotificationPage(IReadOnlyList<NotificationView> Items, bool Unread, string? Next)
{
    public const int DefaultSize = 20;
    public const int MaxSize = 50;

    /// <summary>What the bell shows, and what counts as unread (FR-020).</summary>
    public const int Days = 90;
}

/// <param name="LatestAt">When the newest notification was raised; a console that sees it change fetches the list.</param>
public sealed record BellState(bool Unread, DateTimeOffset? LatestAt);

/// <summary>
/// A person's own notifications (spec 008, FR-020 to FR-028). Every operation is about the
/// account given and no other: another person's notification is "not found".
/// </summary>
public interface IBell
{
    Task<BellState> StateAsync(Guid accountId, CancellationToken cancellation);

    /// <summary>Refused with <c>VALIDATION_FAILED</c> for a <paramref name="before"/> or size that cannot be read.</summary>
    /// <param name="before">The <c>Next</c> of the page before; null for the newest.</param>
    Task<NotificationPage> ListAsync(Guid accountId, string? before, int size, string language, CancellationToken cancellation);

    /// <summary>Marks one read; again changes nothing. <c>NOT_FOUND</c> when it is not this person's.</summary>
    Task MarkReadAsync(Guid accountId, Guid notificationId, CancellationToken cancellation);

    Task MarkAllReadAsync(Guid accountId, CancellationToken cancellation);
}

/// <summary>A clinic's switch for the email of one event, as the settings page lists it.</summary>
public sealed record EmailSwitchView(string Event, string Name, string Group, bool Enabled);

/// <summary>
/// The clinic's email switches (spec 008, FR-037 to FR-041), for the established clinic. Whether
/// the caller may read or change them is the route's business.
/// </summary>
public interface IEmailSwitches
{
    Task<IReadOnlyList<EmailSwitchView>> ListAsync(string language, CancellationToken cancellation);

    /// <summary>
    /// Turns the email of one event on or off for the whole clinic, and records the change in
    /// the audit trail. <c>NOT_FOUND</c> for an event that does not exist;
    /// <c>VALIDATION_FAILED</c> with <c>event: NOT_SWITCHABLE</c> for one whose email is always
    /// or never sent.
    /// </summary>
    Task<EmailSwitchView> SetAsync(string eventKey, bool enabled, string language, CancellationToken cancellation);
}
