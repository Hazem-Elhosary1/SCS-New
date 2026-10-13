using System.Text.RegularExpressions;
using Scs.BuildingBlocks.Mail;
using Scs.Notifications.Infrastructure;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;
using Scs.SharedKernel.Notifications;
using Scs.SharedKernel.Wording;

namespace Scs.UnitTests.Notifications;

/// <summary>
/// Spec 008, FR-001 to FR-004, FR-030, FR-045: the table of notification events is the BRD's
/// (Chapter 13 §9-1), its keys are frozen, and every event says who is told and by which way.
/// </summary>
public partial class NotificationEventsTests
{
    private static readonly (string Key, string Group, RecipientRuleKind Rule, string? Permission, NotificationEmail Email, string Mark)[] Expected =
    [
        ("ticket.support-replied", "support", RecipientRuleKind.OwnerAndPermission, Permissions.TicketsViewAll, NotificationEmail.Always, "ticket"),
        ("ticket.awaiting-reply", "support", RecipientRuleKind.Owner, null, NotificationEmail.Always, "ticket"),
        ("ticket.resolved", "support", RecipientRuleKind.Owner, null, NotificationEmail.Always, "ticket"),
        ("invoice.issued", "billing", RecipientRuleKind.Permission, Permissions.SubscriptionInvoicesView, NotificationEmail.Switch, "invoice"),
        ("payment.received", "billing", RecipientRuleKind.Permission, Permissions.SubscriptionInvoicesView, NotificationEmail.Switch, "invoice"),
        ("payment.failed", "billing", RecipientRuleKind.Permission, Permissions.SubscriptionInvoicesView, NotificationEmail.Always, "warning"),
        ("subscription.ending", "billing", RecipientRuleKind.Permission, Permissions.SubscriptionInvoicesView, NotificationEmail.Switch, "subscription"),
        ("subscription.state-changed", "billing", RecipientRuleKind.Everyone, null, NotificationEmail.Always, "subscription"),
        ("channel.disconnected", "channels", RecipientRuleKind.Permission, Permissions.SettingsChannels, NotificationEmail.Always, "warning"),
        ("messages.balance-exhausted", "channels", RecipientRuleKind.Permission, Permissions.SettingsChannels, NotificationEmail.Always, "warning"),
        ("booking-page.stopped", "services", RecipientRuleKind.Permission, Permissions.SettingsBookingPage, NotificationEmail.Always, "warning"),
        ("teleconsult.minutes-exhausted", "services", RecipientRuleKind.Permission, Permissions.SettingsTeleconsult, NotificationEmail.Always, "warning"),
        ("xray.analyses-exhausted", "services", RecipientRuleKind.Permission, Permissions.SettingsAi, NotificationEmail.Always, "warning"),
        ("call.refunded", "services", RecipientRuleKind.Permission, Permissions.MoneyRefund, NotificationEmail.Always, "notice"),
        ("offer.expired", "patients", RecipientRuleKind.Permission, Permissions.BookingsManage, NotificationEmail.Never, "notice"),
        ("recall.due", "patients", RecipientRuleKind.Permission, Permissions.PatientsRecallLog, NotificationEmail.Never, "notice"),
        ("result.out-of-range", "patients", RecipientRuleKind.Owner, null, NotificationEmail.Never, "warning"),
        ("account.new-device", "accounts", RecipientRuleKind.Owner, null, NotificationEmail.Always, "warning"),
        ("account.support-action", "accounts", RecipientRuleKind.Permission, Permissions.AdminUsersManage, NotificationEmail.Never, "notice"),
        ("licence.expiring", "accounts", RecipientRuleKind.OwnerAndPermission, Permissions.AdminLicencesManage, NotificationEmail.Switch, "warning"),
    ];

    [GeneratedRegex(@"\p{IsArabic}")]
    private static partial Regex ArabicLetter();

    [Fact]
    public void The_table_holds_exactly_the_twenty_events_of_the_BRD_with_who_is_told_and_how()
    {
        // And the two a later spec added the way the table says to: spec 009's, for a file
        // its uploader sent that the check refused.
        (string, string, RecipientRuleKind, string?, NotificationEmail, string)[] added =
        [
            ("file.scan-refused", "accounts", RecipientRuleKind.Owner, null, NotificationEmail.Never, "warning"),
            ("file.unreadable", "accounts", RecipientRuleKind.Owner, null, NotificationEmail.Never, "warning"),
        ];

        Assert.Equal(20, Expected.Length);
        Assert.Equal(
            Expected.Concat(added).OrderBy(e => e.Item1, StringComparer.Ordinal),
            NotificationEvents.All
                .Select(e => (e.Key, e.Group, e.Recipients.Kind, e.Recipients.Permission, e.Email, e.Mark))
                .OrderBy(e => e.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void Keys_are_unique_and_well_formed_and_groups_and_marks_are_known()
    {
        var keys = NotificationEvents.All.Select(e => e.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.Matches(@"^[a-z]+(-[a-z]+)*\.[a-z]+(-[a-z]+)*$", key));
        Assert.All(keys, key => Assert.True(key.Length <= 64, key));
        Assert.All(NotificationEvents.All, e => Assert.Contains(e.Group, NotificationGroups.All));
        Assert.All(NotificationEvents.All, e => Assert.Contains(e.Mark, NotificationMarks.All));
        Assert.Equal(6, NotificationGroups.All.Count);
    }

    [Fact]
    public void Every_key_of_the_table_is_frozen_and_no_frozen_key_is_gone()
    {
        var locked = NotificationEvents.Locked();
        var keys = NotificationEvents.All.Select(e => e.Key).ToList();

        var notFrozen = keys.Except(locked, StringComparer.Ordinal).ToList();
        var gone = locked.Except(keys, StringComparer.Ordinal).ToList();

        Assert.True(notFrozen.Count == 0, "Add these to notification-events.lock, on purpose: " + string.Join(", ", notFrozen));
        Assert.True(gone.Count == 0, "Frozen keys that are missing from the table: " + string.Join(", ", gone));
    }

    [Fact]
    public void Every_event_has_its_name_in_both_languages()
    {
        Assert.All(NotificationEvents.All, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.NameAr), e.Key);
            Assert.False(string.IsNullOrWhiteSpace(e.NameEn), e.Key);
            Assert.Matches(ArabicLetter(), e.NameAr);
            Assert.DoesNotMatch(ArabicLetter(), e.NameEn);
        });
    }

    [Fact]
    public void An_event_that_has_a_line_has_it_in_both_languages()
    {
        Assert.All(NotificationEvents.All, e =>
            Assert.Equal(string.IsNullOrWhiteSpace(e.LineAr), string.IsNullOrWhiteSpace(e.LineEn)));

        // Only the events that have a cause have a line; the others get theirs with their cause.
        Assert.Equal(
            ["account.new-device", "file.scan-refused", "file.unreadable"],
            NotificationEvents.All.Where(e => e.HasLine).Select(e => e.Key));
    }

    [Fact]
    public void Recipients_are_named_by_a_permission_of_the_catalogue_or_by_ownership_never_by_a_role()
    {
        Assert.All(NotificationEvents.All, e =>
        {
            var needsPermission = e.Recipients.Kind is RecipientRuleKind.Permission or RecipientRuleKind.OwnerAndPermission;
            Assert.Equal(needsPermission, e.Recipients.Permission is not null);
            if (e.Recipients.Permission is { } code)
            {
                Assert.True(PermissionCatalogue.Contains(code), $"{e.Key}: {code}");
            }
        });
    }

    [Fact]
    public void Exactly_four_emails_can_be_switched_off_by_a_clinic()
    {
        Assert.Equal(
            ["invoice.issued", "licence.expiring", "payment.received", "subscription.ending"],
            NotificationEvents.Switchable.Select(e => e.Key).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Reminders_repeat_on_the_days_the_BRD_gives()
    {
        Assert.Equal([14, 7, 1], NotificationEvents.Find(NotificationEvents.SubscriptionEnding)!.DaysBefore);
        Assert.Equal([60, 30, 7], NotificationEvents.Find(NotificationEvents.LicenceExpiring)!.DaysBefore);
        Assert.All(
            NotificationEvents.All.Where(e => e.Key is not ("subscription.ending" or "licence.expiring")),
            e => Assert.Empty(e.DaysBefore));
    }

    [Fact]
    public void Nothing_clinical_is_ever_emailed_or_carries_a_fact()
    {
        var clinical = Assert.Single(NotificationEvents.All, e => e.Clinical);

        Assert.Equal("result.out-of-range", clinical.Key);
        Assert.Equal(NotificationEmail.Never, clinical.Email);
        Assert.Empty(clinical.Facts);

        // The patients' group is inside the console only (ق-٣٣).
        Assert.All(
            NotificationEvents.All.Where(e => e.Group == NotificationGroups.Patients),
            e => Assert.Equal(NotificationEmail.Never, e.Email));
    }

    [Fact]
    public void No_event_allows_a_fact_that_could_hold_a_secret_or_an_email_address()
    {
        var offenders = NotificationEvents.All
            .SelectMany(e => e.Facts.Select(fact => (e.Key, Fact: fact)))
            .Where(x => AuditActions.NeverKeys.Any(never => x.Fact.Contains(never, StringComparison.OrdinalIgnoreCase)))
            .Select(x => $"{x.Key}: {x.Fact}")
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(", ", offenders));
    }

    [Fact]
    public void An_event_is_found_by_its_key_and_a_stranger_is_not()
    {
        Assert.Equal("accounts", NotificationEvents.Find(NotificationEvents.AccountNewDevice)?.Group);
        Assert.Null(NotificationEvents.Find("refund.issued"));
        Assert.Null(new NotificationCatalogue().Find("no.such-event"));
    }
}

/// <summary>
/// Spec 008, User Story 8 (FR-019, FR-033, FR-034, FR-045, FR-046): a notification is stored as
/// facts and read as words, in Arabic or in English, in the bell and in its email.
/// </summary>
public partial class NotificationWordsTests
{
    private const string Chrome =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private static readonly NotificationEvent NewDevice = NotificationEvents.Find(NotificationEvents.AccountNewDevice)!;

    private static readonly NotificationEvent Invoice = new(
        "test.invoice", NotificationGroups.Billing, "صدرت فاتورة التجديد", "Renewal invoice issued", NotificationMarks.Invoice,
        RecipientRule.Owner, NotificationEmail.Always, [], ["days", "who"],
        "الفاتورة {label} · تستحق خلال {days} أيام · {who}", "Invoice {label} · due in {days} days · {who}");

    [GeneratedRegex(@"\p{IsArabic}")]
    private static partial Regex ArabicLetter();

    private static string Facts(string json) => json;

    [Fact]
    public void The_title_is_the_events_name_in_the_readers_language()
    {
        Assert.All(NotificationEvents.All, known =>
        {
            Assert.Equal(known.NameAr, NotificationWords.Title(known, known.Key, "ar"));
            Assert.Equal(known.NameEn, NotificationWords.Title(known, known.Key, "en"));
        });

        Assert.Equal("الدخول من جهاز جديد", NotificationWords.Title(NewDevice, NewDevice.Key, "ar"));
        Assert.Equal("Sign-in from a new device", NotificationWords.Title(NewDevice, NewDevice.Key, "en"));
    }

    [Theory]
    [InlineData("2026-10-11T12:04:00Z", "١١ أكتوبر ٢٠٢٦، ٣:٠٤ م", "11 October 2026, 3:04 PM")]
    [InlineData("2026-01-31T22:30:00Z", "١ فبراير ٢٠٢٦، ١٢:٣٠ ص", "1 February 2026, 12:30 AM")]
    [InlineData("2026-12-05T10:00:00Z", "٥ ديسمبر ٢٠٢٦، ١٢:٠٠ م", "5 December 2026, 12:00 PM")]
    public void A_moment_is_written_in_cairo_time_as_each_language_writes_it(string instant, string arabic, string english)
    {
        var moment = DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(arabic, Words.Moment(moment, english: false));
        Assert.Equal(english, Words.Moment(moment, english: true));
    }

    private static EmailMessage Render(EmailContent content) => EmailRenderer.Render(content, Mail.EmailRendererTests.Full);

    [Fact]
    public void An_email_says_when_between_the_line_and_the_way_in()
    {
        var email = Render(NotificationEmails.Notification(
            "a@b.example", "en", "Title", "Line", "https://app.scs.test/", "11 October 2026, 3:04 PM"));

        Assert.Equal(
            ["NOTIFICATION", "Title", "", "Line", "", "When: 11 October 2026, 3:04 PM", "", "Open in Smart Clinic OS:", "https://app.scs.test/"],
            email.TextBody.Split('\n').Take(9));
        Assert.Contains("11 October 2026, 3:04 PM", email.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void An_email_writes_western_digits_whatever_the_bell_writes()
    {
        var email = Render(NotificationEmails.Notification(
            "a@b.example", "ar", "تستحق خلال ٧ أيام", "الفاتورة INV-2026-0012 · تستحق خلال ٧ أيام", "https://app.scs.test/", "١١ أكتوبر ٢٠٢٦، ٣:٠٤ م"));

        Assert.Equal("تستحق خلال 7 أيام", email.Subject);
        Assert.Contains("الوقت: 11 أكتوبر 2026، 3:04 م", email.TextBody, StringComparison.Ordinal);
        Assert.DoesNotMatch("[٠-٩]", email.Subject + email.TextBody + email.HtmlBody);
    }

    [Theory]
    [InlineData("١١ أكتوبر ٢٠٢٦، ٣:٠٤ م", "11 أكتوبر 2026، 3:04 م")]
    [InlineData("۱۲ فاتورة · ۰۹۸۷۶۵۴۳", "12 فاتورة · 09876543")]
    [InlineData("٠١٢٣٤٥٦٧٨٩ ۰۱۲۳۴۵۶۷۸۹", "0123456789 0123456789")]
    [InlineData("INV-2026-0012 · 7 days", "INV-2026-0012 · 7 days")]
    [InlineData("", "")]
    public void Western_digits_are_written_for_both_ranges_of_arabic_digits(string written, string western)
    {
        Assert.Equal(western, Words.Western(written));

        var email = Render(NotificationEmails.Notification("a@b.example", "ar", "عنوان", written, "https://app.scs.test/"));
        Assert.DoesNotMatch("[٠-٩۰-۹]", email.TextBody + email.HtmlBody);
    }

    [Fact]
    public void A_long_title_is_cut_at_a_word_in_the_subject_and_nothing_empty_is_drawn()
    {
        var title = string.Join(' ', Enumerable.Repeat("reminder", 20));
        var content = NotificationEmails.Notification("a@b.example", "en", title, "", "https://app.scs.test/");

        Assert.True(content.Subject.Length <= 120);
        Assert.EndsWith("reminder…", content.Subject, StringComparison.Ordinal);
        Assert.Equal(title, content.Title);
        Assert.DoesNotContain("data-part=\"paragraph\"", Render(content).HtmlBody, StringComparison.Ordinal);
        Assert.Empty(content.Parts.OfType<EmailFacts>());
    }

    [Fact]
    public void A_sign_in_from_a_new_device_says_the_device_in_words()
    {
        var facts = Facts($$"""{"device":"{{Chrome}}"}""");

        Assert.Equal("متصفح Chrome على Windows", NotificationWords.Line(NewDevice, null, facts, "ar"));
        Assert.Equal("Chrome on Windows", NotificationWords.Line(NewDevice, null, facts, "en"));
        Assert.Equal("جهاز غير معروف", NotificationWords.Line(NewDevice, null, """{"device":null}""", "ar"));
        Assert.Equal("Unknown device", NotificationWords.Line(NewDevice, null, """{"device":"curl/8"}""", "en"));
    }

    [Fact]
    public void A_line_keeps_identifiers_latin_writes_numbers_as_the_language_does_and_leaves_names_as_written()
    {
        var facts = Facts("""{"days":7,"who":"د. سارة يوسف"}""");

        Assert.Equal(
            "الفاتورة INV-2026-0012 · تستحق خلال ٧ أيام · د. سارة يوسف",
            NotificationWords.Line(Invoice, "INV-2026-0012", facts, "ar"));
        Assert.Equal(
            "Invoice INV-2026-0012 · due in 7 days · د. سارة يوسف",
            NotificationWords.Line(Invoice, "INV-2026-0012", facts, "en"));
    }

    [Fact]
    public void An_english_line_has_no_arabic_in_it_but_what_a_person_wrote()
    {
        var english = NotificationWords.Line(Invoice, "INV-2026-0012", """{"days":14,"who":"Front desk"}""", "en");
        var arabic = NotificationWords.Line(Invoice, "INV-2026-0012", """{"days":14,"who":"الاستقبال"}""", "ar");

        Assert.DoesNotMatch(ArabicLetter(), english);
        Assert.Contains("١٤", arabic, StringComparison.Ordinal);
        Assert.DoesNotContain("14", arabic.Replace("INV-2026-0012", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("{", english + arabic, StringComparison.Ordinal);
    }

    [Fact]
    public void An_event_nobody_knows_any_more_still_reads_as_something()
    {
        Assert.Equal("gone.event", NotificationWords.Title(null, "gone.event", "ar"));
        Assert.Equal("T-1046", NotificationWords.Line(null, "T-1046", null, "en"));
        Assert.Equal("", NotificationWords.Line(null, null, null, "ar"));
    }

    [Theory]
    [InlineData("ar", "rtl", "فتح في سمارت كلينك أو إس", "إشعار", "أُرسلت هذه الرسالة تلقائيًّا، ولا تُقرأ الردود عليها.", "وصلتك هذه الرسالة لأن تنبيهات البريد مفعّلة لحسابك في سمارت كلينك أو إس.")]
    [InlineData("en", "ltr", "Open in Smart Clinic OS", "NOTIFICATION", "This message was sent automatically; replies to it are not read.", "You’re receiving this email because email notifications are on for your Smart Clinic OS account.")]
    public void An_email_is_in_one_language_and_says_only_what_the_bell_says(
        string language, string direction, string open, string eyebrow, string note, string reason)
    {
        var title = NotificationWords.Title(NewDevice, NewDevice.Key, language);
        var line = NotificationWords.Line(NewDevice, null, Facts($$"""{"device":"{{Chrome}}"}"""), language);

        var content = NotificationEmails.Notification("sara@noor.example", language, title, line, "https://app.scs.test/");
        EmailMessage email = Render(content);

        Assert.Equal(("sara@noor.example", title, eyebrow), (email.To, email.Subject, content.Eyebrow));
        Assert.Contains(title, email.TextBody, StringComparison.Ordinal);
        Assert.Contains(line, email.TextBody, StringComparison.Ordinal);
        Assert.Contains($"{open}:\nhttps://app.scs.test/", email.TextBody, StringComparison.Ordinal);
        Assert.Contains(note, email.TextBody, StringComparison.Ordinal);
        Assert.Contains(reason, email.TextBody, StringComparison.Ordinal);
        Assert.Contains($"<html lang=\"{language}\" dir=\"{direction}\">", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("href=\"https://app.scs.test/\"", email.HtmlBody, StringComparison.Ordinal);

        // One language: the other one's words are nowhere in it, and neither is the address.
        if (language == "en")
        {
            Assert.DoesNotMatch(ArabicLetter(), email.TextBody);
        }
        else
        {
            Assert.DoesNotContain("Smart Clinic OS", email.TextBody, StringComparison.Ordinal);
            Assert.DoesNotContain("Open in", email.TextBody, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("sara@noor.example", email.TextBody + email.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void What_a_person_wrote_cannot_break_out_of_the_email()
    {
        var email = Render(NotificationEmails.Notification(
            "a@b.example", "en", "Title", "<script>alert(1)</script> & co", "https://app.scs.test/x"));

        Assert.DoesNotContain("<script>", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", email.HtmlBody, StringComparison.Ordinal);
    }
}
