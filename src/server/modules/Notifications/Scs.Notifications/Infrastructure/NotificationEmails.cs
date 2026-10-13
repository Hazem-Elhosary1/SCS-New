using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Jobs;
using Scs.BuildingBlocks.Mail;
using Scs.BuildingBlocks.Notifications;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Identity.Contracts;
using Scs.Notifications.Infrastructure.Persistence;
using Scs.SharedKernel.Notifications;
using Scs.SharedKernel.Wording;

namespace Scs.Notifications.Infrastructure;

/// <summary>
/// What a notification's email says (spec 008, FR-033, FR-034; spec 094): one language, the
/// event's title as its subject, the line, when it happened, and a way to the same place the
/// bell leads. Only the words are here — the layout and the footer are the template's. It names
/// a record by its identifier and holds nothing clinical, no password and no code. The words
/// are in specs/094-email-template/contracts/emails.md §B.
/// </summary>
internal static class NotificationEmails
{
    private const string Platform = EmailContent.Platform;

    /// <param name="title">The event's title, as the bell shows it.</param>
    /// <param name="line">The event's line; left out when empty.</param>
    /// <param name="when">When it happened, in words; left out when empty.</param>
    public static EmailContent Notification(string to, string language, string title, string line, string address, string when = "")
    {
        var english = language == Words.English;

        // An email writes Western digits in both languages (spec 094, FR-011); the bell keeps its own.
        title = Words.Western(title);
        var parts = new List<EmailPart> { new EmailParagraph(Words.Western(line ?? "")) };

        if (!string.IsNullOrWhiteSpace(when))
        {
            parts.Add(new EmailFacts([new EmailFact(english ? "When" : "الوقت", Words.Western(when))]));
        }

        parts.Add(new EmailButton(english ? $"Open in {Platform}" : $"فتح في {Platform}", address));
        parts.Add(new EmailNote(english
            ? "This message was sent automatically; replies to it are not read."
            : "أُرسلت هذه الرسالة تلقائيًّا، ولا تُقرأ الردود عليها."));

        return new(
            to, english ? "en" : "ar", title, english ? "NOTIFICATION" : "إشعار", title, parts,
            english
                ? $"You’re receiving this email because email notifications are on for your {Platform} account."
                : $"وصلتك هذه الرسالة لأن تنبيهات البريد مفعّلة لحسابك في {Platform}.");
    }
}

/// <summary>
/// Sends the clinic's notification emails that are waiting (FR-031, FR-035, FR-035a). The
/// address and the language are read from the account at the moment of sending, so an account
/// that can no longer sign in gets nothing. An email the mail server does not take is tried
/// again after one, five and thirty minutes and then every hour, and given up a day after it was
/// owed. Running it twice sends nothing twice.
/// </summary>
[RecordsNothing("sends the emails that are owed for notifications; the cause of each is recorded where it happened")]
internal sealed partial class NotificationEmailJob(
    NotificationsDbContext db,
    IStaffDirectory directory,
    INotificationCatalogue catalogue,
    IEmailSender mail,
    EmailRenderer renderer,
    TimeProvider clock,
    IOptions<ScsOptions> options,
    ILogger<NotificationEmailJob> logger) : IClinicJob
{
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromHours(24);

    /// <summary>How long to wait after each failed try; the last figure repeats.</summary>
    public static readonly IReadOnlyList<TimeSpan> Waits =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)];

    /// <summary>One run sends at most this many, so one clinic's backlog does not hold the worker.</summary>
    public const int Batch = 200;

    public async Task RunAsync(CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var due = await db.Emails
            .Include(e => e.Notification)
            .Where(e => e.State == NotifySql.Waiting && e.NextAttemptAtUtc <= now)
            .OrderBy(e => e.NextAttemptAtUtc)
            .Take(Batch)
            .ToListAsync(cancellation)
            .ConfigureAwait(false);

        if (due.Count == 0)
        {
            return;
        }

        var contacts = await directory
            .ContactsAsync([.. due.Select(e => e.Notification!.RecipientAccountId).Distinct()], cancellation)
            .ConfigureAwait(false);

        var sent = 0;
        var dropped = 0;
        var failed = 0;
        var gaveUp = 0;

        foreach (var email in due)
        {
            var notification = email.Notification!;
            now = clock.GetUtcNow();

            // Checked now, not when the event was raised: an account disabled since gets nothing.
            if (!contacts.TryGetValue(notification.RecipientAccountId, out var contact) || !contact.CanSignIn)
            {
                email.Settle(NotifySql.Dropped, now);
                dropped++;
            }
            else
            {
                var language = Words.Language(contact.Language);
                var known = catalogue.Find(notification.EventKey);
                try
                {
                    var message = renderer.Render(NotificationEmails.Notification(
                        contact.Email,
                        language,
                        NotificationWords.Title(known, notification.EventKey, language),
                        NotificationWords.Line(known, notification.SubjectLabel, notification.Facts, language),
                        options.Value.Auth.StaffAppBase() + "/" + (notification.Link ?? "").TrimStart('/'),
                        Words.Moment(notification.RaisedAtUtc, language == Words.English)));

                    await mail.SendAsync(message, cancellation).ConfigureAwait(false);
                    email.Settle(NotifySql.Sent, now);
                    sent++;
                }
                catch (EmailCompositionException)
                {
                    // It can never be whole, so it is not tried again and holds up nobody else's.
                    email.Settle(NotifySql.GaveUp, now);
                    gaveUp++;
                }
                catch (EmailNotSentException)
                {
                    if (now - email.CreatedAtUtc >= GiveUpAfter)
                    {
                        email.Settle(NotifySql.GaveUp, now);
                        gaveUp++;
                    }
                    else
                    {
                        email.Postpone(now + Waits[Math.Min(email.Attempts, Waits.Count - 1)]);
                        failed++;
                    }
                }
            }

            // One at a time: a later failure does not un-send what already left.
            await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }

        LogHandled(logger, sent, dropped, failed, gaveUp);
    }

    // Counts only: an address never reaches the logs (SH-094).
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Notification emails: {sent} sent, {dropped} dropped, {failed} to try again, {gaveUp} given up")]
    private static partial void LogHandled(ILogger logger, int sent, int dropped, int failed, int gaveUp);
}

/// <summary>
/// Every minute: finds the clinics that have notification email due and enqueues the clinic's
/// job for each, so a retry does not wait for the clinic's next event. It crosses the clinic
/// boundary to read clinic ids and nothing else, and is declared as such.
/// </summary>
[RecordsNothing("finds clinics with email waiting and enqueues their job; changes nothing")]
internal sealed class EmailSweepJob(ICrossClinicConnectionFactory crossing, IClinicJobScheduler scheduler, TimeProvider clock)
{
    public const string JobName = "notify-email-sweep";
    public const string Cron = "* * * * *";

    public async Task RunAsync(CancellationToken cancellation)
    {
        var builder = new DbContextOptionsBuilder<NotificationsDbContext>();
        crossing.Configure(builder);
        var db = new NotificationsDbContext(builder.Options, new ClinicContext());
        await using (db.ConfigureAwait(false))
        {
            var now = clock.GetUtcNow();

            // No clinic is established here: the application's own filter would match nothing.
            var clinics = await db.Emails.AsNoTracking().IgnoreQueryFilters()
                .Where(e => e.State == NotifySql.Waiting && e.NextAttemptAtUtc <= now)
                .Select(e => e.ClinicId)
                .Distinct()
                .ToListAsync(cancellation)
                .ConfigureAwait(false);

            foreach (var clinicId in clinics)
            {
                scheduler.TryEnqueueForClinic<NotificationEmailJob>(clinicId);
            }
        }
    }
}

/// <summary>
/// The daily job <c>notify-cleanup</c>, for each clinic in turn: removes notifications older
/// than ninety days, read or not, and the records of their emails with them (FR-019b). A
/// notification points at something that is recorded where it happened, so nothing is lost.
/// Running it twice has the same result as once.
/// </summary>
[RecordsNothing("removes notifications older than ninety days; each points at a record kept where it happened")]
internal sealed partial class NotificationCleanupJob(
    NotificationsDbContext db, TimeProvider clock, ILogger<NotificationCleanupJob> logger) : IClinicJob
{
    public const string JobName = "notify-cleanup";
    public const string Cron = "15 2 * * *";

    public static readonly TimeSpan Keep = TimeSpan.FromDays(Contracts.NotificationPage.Days);

    public async Task RunAsync(CancellationToken cancellation)
    {
        var before = clock.GetUtcNow() - Keep;

        // The emails go with their notifications (cascade).
        var removed = await db.Notifications.Where(n => n.RaisedAtUtc < before)
            .ExecuteDeleteAsync(cancellation).ConfigureAwait(false);

        LogCleaned(logger, removed);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed {removed} notifications older than ninety days")]
    private static partial void LogCleaned(ILogger logger, int removed);
}

/// <summary>Has the clinic's email job run as soon as a save left email waiting.</summary>
internal sealed class NotificationDispatch(IClinicJobScheduler scheduler) : INotificationDispatch
{
    public void EmailsWaiting(Guid clinicId) => scheduler.TryEnqueueForClinic<NotificationEmailJob>(clinicId);
}
