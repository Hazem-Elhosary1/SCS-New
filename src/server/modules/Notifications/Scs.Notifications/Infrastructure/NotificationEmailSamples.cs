using Scs.BuildingBlocks.Mail;

namespace Scs.Notifications.Infrastructure;

/// <summary>
/// A notification's email with made-up values, in both languages, for the catalogue (spec 094,
/// FR-037, FR-039). Read by the tests that export and check every email; nothing that runs
/// sends it.
/// </summary>
internal sealed class NotificationEmailSamples : IEmailSamples
{
    private const string To = "sara@noor.example";
    private const string Address = "https://app.scs.test/profile/sessions";

    public IReadOnlyList<EmailSample> All() =>
    [
        new("Notification", EmailSample.Clinic, NotificationEmails.Notification(
            To, "ar", "الدخول من جهاز جديد", "متصفح Chrome على Windows", Address, "١٢ أكتوبر ٢٠٢٦، ١٢:٠٤ م")),
        new("Notification", EmailSample.Clinic, NotificationEmails.Notification(
            To, "en", "Sign-in from a new device", "Chrome on Windows", Address, "12 October 2026, 12:04 PM")),
    ];
}
