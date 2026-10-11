using System.Reflection;
using Scs.SharedKernel.Access;

namespace Scs.SharedKernel.Notifications;

/// <summary>The six groups the clinic's «التنبيهات» page lists events under (BRD Chapter 13 §3-5).</summary>
public static class NotificationGroups
{
    public const string Support = "support";
    public const string Billing = "billing";
    public const string Channels = "channels";
    public const string Services = "services";
    public const string Patients = "patients";
    public const string Accounts = "accounts";

    public static IReadOnlyList<string> All { get; } = [Support, Billing, Channels, Services, Patients, Accounts];
}

/// <summary>How the bell draws a notification: its icon and tint (CL-SH-032).</summary>
public static class NotificationMarks
{
    public const string Ticket = "ticket";
    public const string Invoice = "invoice";
    public const string Subscription = "subscription";
    public const string Feedback = "feedback";
    public const string Notice = "notice";
    public const string Warning = "warning";
    public const string Incident = "incident";

    public static IReadOnlyList<string> All { get; } = [Ticket, Invoice, Subscription, Feedback, Notice, Warning, Incident];
}

/// <summary>Whether an event is also emailed (ق-٣٢).</summary>
public enum NotificationEmail
{
    /// <summary>Urgent: nothing a clinic can set stops it.</summary>
    Always = 0,

    /// <summary>While the clinic keeps its switch for the event on.</summary>
    Switch = 1,

    /// <summary>Inside the console only. Every clinical event is this.</summary>
    Never = 2,
}

public enum RecipientRuleKind
{
    /// <summary>Everyone whose role holds one permission.</summary>
    Permission = 0,

    /// <summary>The people the caller names: the owner of the record, or whoever the event is for.</summary>
    Owner = 1,

    /// <summary>Both of the above, each person once.</summary>
    OwnerAndPermission = 2,

    /// <summary>Everyone in the clinic.</summary>
    Everyone = 3,
}

/// <summary>Who is told. By a permission or by ownership, never by the name of a role (ق-٣٤).</summary>
public sealed record RecipientRule(RecipientRuleKind Kind, string? Permission = null)
{
    public static RecipientRule Owner { get; } = new(RecipientRuleKind.Owner);

    public static RecipientRule Everyone { get; } = new(RecipientRuleKind.Everyone);

    public static RecipientRule Holders(string permission) => new(RecipientRuleKind.Permission, permission);

    public static RecipientRule OwnerAnd(string permission) => new(RecipientRuleKind.OwnerAndPermission, permission);

    public bool NamesAnOwner => Kind is RecipientRuleKind.Owner or RecipientRuleKind.OwnerAndPermission;
}

/// <summary>
/// One event people are told about (spec 008, FR-001). The key never changes once used. The
/// line and the destination are absent until the event has a cause: the spec that builds the
/// cause writes them, and an event without a line cannot be raised.
/// </summary>
/// <param name="LineAr">The second line of a bell item: a template with <c>{label}</c> and the event's fact keys.</param>
/// <param name="Destination">A path inside the console, with <c>{id}</c>; null when it leads nowhere.</param>
/// <param name="Clinical">Carries no fact at all, and is never emailed (ق-٣٣, SH-070).</param>
public sealed record NotificationEvent(
    string Key,
    string Group,
    string NameAr,
    string NameEn,
    string Mark,
    RecipientRule Recipients,
    NotificationEmail Email,
    IReadOnlyList<int> DaysBefore,
    IReadOnlyList<string> Facts,
    string? LineAr = null,
    string? LineEn = null,
    string? Destination = null,
    bool Clinical = false)
{
    public bool HasLine => !string.IsNullOrWhiteSpace(LineAr) && !string.IsNullOrWhiteSpace(LineEn);
}

/// <summary>
/// The table of notification events (spec 008; BRD SH-072, Chapter 13 §9-1). A later spec gives
/// an event its line where it builds the event's cause, or adds its own event here and to
/// <c>notification-events.lock</c>, and nowhere else.
/// </summary>
public static class NotificationEvents
{
    public const string TicketSupportReplied = "ticket.support-replied";
    public const string TicketAwaitingReply = "ticket.awaiting-reply";
    public const string TicketResolved = "ticket.resolved";
    public const string InvoiceIssued = "invoice.issued";
    public const string PaymentReceived = "payment.received";
    public const string PaymentFailed = "payment.failed";
    public const string SubscriptionEnding = "subscription.ending";
    public const string SubscriptionStateChanged = "subscription.state-changed";
    public const string ChannelDisconnected = "channel.disconnected";
    public const string MessagesBalanceExhausted = "messages.balance-exhausted";
    public const string BookingPageStopped = "booking-page.stopped";
    public const string TeleconsultMinutesExhausted = "teleconsult.minutes-exhausted";
    public const string XrayAnalysesExhausted = "xray.analyses-exhausted";
    public const string CallRefunded = "call.refunded";
    public const string OfferExpired = "offer.expired";
    public const string RecallDue = "recall.due";
    public const string ResultOutOfRange = "result.out-of-range";
    public const string AccountNewDevice = "account.new-device";
    public const string AccountSupportAction = "account.support-action";
    public const string LicenceExpiring = "licence.expiring";

    private static readonly int[] None = [];

    public static IReadOnlyList<NotificationEvent> All { get; } =
    [
        // ن-٠١ … ن-٠٣ — support and tickets
        new(TicketSupportReplied, NotificationGroups.Support, "ردّ فريق الدعم", "Support replied",
            NotificationMarks.Ticket, RecipientRule.OwnerAnd(Permissions.TicketsViewAll), NotificationEmail.Always, None, []),
        new(TicketAwaitingReply, NotificationGroups.Support, "فريق الدعم ينتظر ردّك", "Support is waiting for your reply",
            NotificationMarks.Ticket, RecipientRule.Owner, NotificationEmail.Always, None, []),
        new(TicketResolved, NotificationGroups.Support, "حُلَّت التذكرة", "Ticket resolved",
            NotificationMarks.Ticket, RecipientRule.Owner, NotificationEmail.Always, None, []),

        // ن-٠٤ … ن-٠٨ — billing and subscription
        new(InvoiceIssued, NotificationGroups.Billing, "صدرت فاتورة", "Invoice issued",
            NotificationMarks.Invoice, RecipientRule.Holders(Permissions.SubscriptionInvoicesView), NotificationEmail.Switch, None, []),
        new(PaymentReceived, NotificationGroups.Billing, "استُلمت الدفعة", "Payment received",
            NotificationMarks.Invoice, RecipientRule.Holders(Permissions.SubscriptionInvoicesView), NotificationEmail.Switch, None, []),
        new(PaymentFailed, NotificationGroups.Billing, "تعذّر الدفع", "Payment failed",
            NotificationMarks.Warning, RecipientRule.Holders(Permissions.SubscriptionInvoicesView), NotificationEmail.Always, None, []),
        new(SubscriptionEnding, NotificationGroups.Billing, "قرب انتهاء الاشتراك", "Subscription ending soon",
            NotificationMarks.Subscription, RecipientRule.Holders(Permissions.SubscriptionInvoicesView), NotificationEmail.Switch, [14, 7, 1], []),
        new(SubscriptionStateChanged, NotificationGroups.Billing, "تغيّر حالة الاشتراك", "Subscription state changed",
            NotificationMarks.Subscription, RecipientRule.Everyone, NotificationEmail.Always, None, []),

        // ن-٠٩, ن-١٠ — channels and messages
        new(ChannelDisconnected, NotificationGroups.Channels, "انقطاع قناة", "Channel disconnected",
            NotificationMarks.Warning, RecipientRule.Holders(Permissions.SettingsChannels), NotificationEmail.Always, None, []),
        new(MessagesBalanceExhausted, NotificationGroups.Channels, "نفاد رصيد الرسائل", "Message balance used up",
            NotificationMarks.Warning, RecipientRule.Holders(Permissions.SettingsChannels), NotificationEmail.Always, None, []),

        // ن-١١ … ن-١٤ — the booking page, remote visits and AI
        new(BookingPageStopped, NotificationGroups.Services, "توقّف صفحة الحجز العامة", "Public booking page stopped",
            NotificationMarks.Warning, RecipientRule.Holders(Permissions.SettingsBookingPage), NotificationEmail.Always, None, []),
        new(TeleconsultMinutesExhausted, NotificationGroups.Services, "نفاد دقائق الكشف عن بُعد", "Remote visit minutes used up",
            NotificationMarks.Warning, RecipientRule.Holders(Permissions.SettingsTeleconsult), NotificationEmail.Always, None, []),
        new(XrayAnalysesExhausted, NotificationGroups.Services, "نفاد تحاليل الأشعة", "X-ray analyses used up",
            NotificationMarks.Warning, RecipientRule.Holders(Permissions.SettingsAi), NotificationEmail.Always, None, []),
        new(CallRefunded, NotificationGroups.Services, "ردّ مبلغ مكالمة لم يبدأها الطبيب", "Call the doctor did not start was refunded",
            NotificationMarks.Notice, RecipientRule.Holders(Permissions.MoneyRefund), NotificationEmail.Always, None, []),

        // ن-١٥ … ن-١٧ — patients and appointments: inside the console only
        new(OfferExpired, NotificationGroups.Patients, "انتهى عرض موعد دون حجز", "Appointment offer expired unbooked",
            NotificationMarks.Notice, RecipientRule.Holders(Permissions.BookingsManage), NotificationEmail.Never, None, []),
        new(RecallDue, NotificationGroups.Patients, "حان موعد التواصل مع مريض", "Patient follow-up is due",
            NotificationMarks.Notice, RecipientRule.Holders(Permissions.PatientsRecallLog), NotificationEmail.Never, None, []),
        new(ResultOutOfRange, NotificationGroups.Patients, "وصلت نتيجة أو قيمة خارج المدى", "A result or value out of range arrived",
            NotificationMarks.Warning, RecipientRule.Owner, NotificationEmail.Never, None, [], Clinical: true),

        // ن-١٨ … ن-٢٠ — accounts and licences
        new(AccountNewDevice, NotificationGroups.Accounts, "الدخول من جهاز جديد", "Sign-in from a new device",
            NotificationMarks.Warning, RecipientRule.Owner, NotificationEmail.Always, None, ["device"],
            LineAr: "{device}", LineEn: "{device}"),
        new(AccountSupportAction, NotificationGroups.Accounts, "إجراء من فريق الدعم على مستخدم", "Support acted on a user",
            NotificationMarks.Notice, RecipientRule.Holders(Permissions.AdminUsersManage), NotificationEmail.Never, None, []),
        new(LicenceExpiring, NotificationGroups.Accounts, "اقتراب انتهاء رخصة", "Licence expiring soon",
            NotificationMarks.Warning, RecipientRule.OwnerAnd(Permissions.AdminLicencesManage), NotificationEmail.Switch, [60, 30, 7], []),
    ];

    private static readonly Dictionary<string, NotificationEvent> ByKey = All.ToDictionary(e => e.Key, StringComparer.Ordinal);

    public static NotificationEvent? Find(string key) => ByKey.GetValueOrDefault(key);

    /// <summary>The events whose email a clinic may turn off: four today (CL-SB-097).</summary>
    public static IReadOnlyList<NotificationEvent> Switchable { get; } = [.. All.Where(e => e.Email == NotificationEmail.Switch)];

    /// <summary>The keys that are frozen: <c>notification-events.lock</c>, one key per line.</summary>
    public static IReadOnlyList<string> Locked()
    {
        var assembly = typeof(NotificationEvents).GetTypeInfo().Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("notification-events.lock", StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);

        return
        [
            .. reader.ReadToEnd()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !line.StartsWith('#')),
        ];
    }
}

/// <summary>Where the engine looks an event up. Replaced in a test that adds an event of its own.</summary>
public interface INotificationCatalogue
{
    NotificationEvent? Find(string key);
}

public sealed class NotificationCatalogue : INotificationCatalogue
{
    public NotificationEvent? Find(string key) => NotificationEvents.Find(key);
}
