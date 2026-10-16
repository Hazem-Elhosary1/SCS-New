using Scs.BuildingBlocks.Mail;

namespace Scs.UnitTests.Mail;

/// <summary>
/// Spec 094, FR-038: the template's own samples. The shell with the frames' placeholder words;
/// the three drawn kinds no spec sends yet — a ticket notification, an invoice, a subscription
/// notice — built from the parts alone with the frames' sample words, as the proof that later
/// specs need no new layout; and the long things an email must hold without breaking.
/// </summary>
public sealed class SampleKinds : IEmailSamples
{
    public const string To = "owner@noor.example";
    private const string App = "https://app.scs.test";

    public IReadOnlyList<EmailSample> All() =>
    [
        Sample("Shell", Shell("ar")), Sample("Shell", Shell("en")),
        Sample("TicketNotification", Ticket("ar")), Sample("TicketNotification", Ticket("en")),
        Sample("Invoice", Invoice("ar")), Sample("Invoice", Invoice("en")),
        Sample("SubscriptionNotice", Subscription("ar")), Sample("SubscriptionNotice", Subscription("en")),
        Sample("LongContent", Lengthy("ar")), Sample("LongContent", Lengthy("en")),
    ];

    private static EmailSample Sample(string kind, EmailContent content) => new(kind, EmailSample.Sample, content);

    /// <summary>Frames <c>451:2</c> and <c>447:3</c>.</summary>
    public static EmailContent Shell(string language) => language == "en"
        ? new(To, "en", "Email subject goes here", "EYEBROW LABEL", "Email Heading Goes Here",
            [
                new EmailParagraph("This is the body copy area — a short paragraph explaining the email’s purpose, written in plain, friendly language."),
                new EmailButton("Call to Action", $"{App}/action"),
            ])
        : new(To, "ar", "موضوع البريد هنا", "تصنيف فرعي", "عنوان البريد الإلكتروني هنا",
            [
                new EmailParagraph("هذه منطقة نص الرسالة — فقرة قصيرة توضح الغرض من البريد الإلكتروني بأسلوب بسيط وودود."),
                new EmailButton("دعوة لاتخاذ إجراء", $"{App}/action"),
            ]);

    /// <summary>Frames <c>455:2</c> and <c>449:2</c>.</summary>
    public static EmailContent Ticket(string language) => language == "en"
        ? new(To, "en", "Your ticket has a new reply", "TICKET UPDATE", "Your ticket has a new reply",
            [
                new EmailParagraph("Support replied to ticket T-1042 from Al Noor Clinic. Take a look and respond if needed."),
                new EmailAttention(
                    "T-1042 — Unable to access the bookings page", "Al Noor Clinic  ·  Opened yesterday",
                    new EmailStatus("Overdue", EmailTone.Warning)),
                new EmailButton("View Ticket", $"{App}/tickets/T-1042"),
            ],
            $"You’re receiving this email because this ticket belongs to Al Noor Clinic on {EmailContent.Platform}.")
        : new(To, "ar", "رد جديد على تذكرتك", "تحديث تذكرة", "رد جديد على تذكرتك",
            [
                new EmailParagraph($"رد فريق الدعم على التذكرة {EmailContent.LeftToRight("T-1042")} من عيادة النور. راجعها ورد عليها إن لزم."),
                new EmailAttention(
                    $"{EmailContent.LeftToRight("T-1042")} — تعذر الوصول إلى صفحة الحجوزات", "عيادة النور  ·  فُتحت أمس",
                    new EmailStatus("متأخرة", EmailTone.Warning)),
                new EmailButton("عرض التذكرة", $"{App}/tickets/T-1042"),
            ],
            $"وصلتك هذه الرسالة لأن هذه التذكرة تخص عيادة النور على {EmailContent.Platform}.");

    /// <summary>Frames <c>2227:268</c> and <c>2227:237</c>.</summary>
    public static EmailContent Invoice(string language) => language == "en"
        ? new(To, "en", "Your invoice is ready", "SUBSCRIPTION INVOICE", "Your invoice is ready",
            [
                new EmailParagraph("Hello Dr. Haitham, here is the subscription invoice for Al Noor Clinic. Pay through Fawry by card, mobile wallet or a Fawry code."),
                new EmailFacts(
                    [new EmailFact("Invoice", "INV-2026-0068"), new EmailFact("Due date", "Oct 25, 2026")],
                    total: new EmailFact("Amount due", "EGP 568.86"), ruled: false),
                new EmailButton("Pay with Fawry", $"{App}/subscription/invoices/INV-2026-0068/pay"),
                new EmailNote("The subscription renews automatically once the payment is confirmed."),
            ],
            $"You’re receiving this email as the owner of Al Noor Clinic on {EmailContent.Platform}.")
        : new(To, "ar", "فاتورتك جاهزة", "فاتورة الاشتراك", "فاتورتك جاهزة",
            [
                new EmailParagraph("مرحبًا د. هيثم، هذه فاتورة اشتراك عيادة النور. ادفع عبر فوري بالبطاقة أو المحفظة الإلكترونية أو كود فوري."),
                new EmailFacts(
                    [
                        new EmailFact("رقم الفاتورة", EmailContent.LeftToRight("INV-2026-0068")),
                        new EmailFact("تاريخ الاستحقاق", "25 أكتوبر 2026"),
                    ],
                    total: new EmailFact("المبلغ المستحق", "568.86 ج.م"), ruled: false),
                new EmailButton("ادفع عبر فوري", $"{App}/subscription/invoices/INV-2026-0068/pay"),
                new EmailNote("يتجدد الاشتراك تلقائيًا فور تأكيد الدفع."),
            ],
            $"وصلتك هذه الرسالة بصفتك مالك عيادة النور على {EmailContent.Platform}.");

    /// <summary>Frames <c>2593:183</c> and <c>2593:71</c>.</summary>
    public static EmailContent Subscription(string language) => language == "en"
        ? new(To, "en", "Al Noor Clinic’s subscription is suspended", "SUBSCRIPTION SUSPENDED", "Al Noor Clinic’s subscription is suspended",
            [
                new EmailParagraph("SCS suspended Al Noor Clinic’s subscription today, Nov 3, 2026, because invoice INV-2026-0068, due Oct 25, is still unpaid: clinic staff can’t sign in and the booking assistant doesn’t answer patients. The clinic’s data is kept as it is."),
                new EmailFacts(
                    [new EmailFact("Professional plan — monthly", "EGP 499.00 + VAT"), new EmailFact("Reason", "Overdue invoice")],
                    total: new EmailFact("Amount due · INV-2026-0068", "EGP 568.86"),
                    status: new EmailStatus("Suspended", EmailTone.Bad, "since Nov 3, 2026")),
                new EmailNote("To reactivate, call or WhatsApp your account manager, Ahmed Sami."),
            ],
            $"You’re receiving this email as the owner of Al Noor Clinic on {EmailContent.Platform}.")
        : new(To, "ar", "أُوقف اشتراك عيادة النور", "اشتراك موقوف", "أُوقف اشتراك عيادة النور",
            [
                new EmailParagraph($"أوقف فريق {EmailContent.LeftToRight("SCS")} اشتراك عيادة النور اليوم 3 نوفمبر 2026 لأن الفاتورة {EmailContent.LeftToRight("INV-2026-0068")} المستحقة في 25 أكتوبر ما زالت غير مسددة، فلا يستطيع موظفو العيادة الدخول، ولا يرد مساعد الحجز على المرضى. تبقى بيانات العيادة محفوظة كما هي."),
                new EmailFacts(
                    [new EmailFact("الباقة الاحترافية — شهري", "499.00 ج.م + الضريبة"), new EmailFact("السبب", "فاتورة متأخرة")],
                    total: new EmailFact($"المستحق · {EmailContent.LeftToRight("INV-2026-0068")}", "568.86 ج.م"),
                    status: new EmailStatus("موقوف", EmailTone.Bad, "منذ 3 نوفمبر 2026")),
                new EmailNote("لإعادة التفعيل تواصل مع مدير حسابك أحمد سامي هاتفيًا أو عبر واتساب."),
            ],
            $"وصلتك هذه الرسالة بصفتك مالك عيادة النور على {EmailContent.Platform}.");

    /// <summary>
    /// What must not break an email: a clinic's name as long as a sentence, a link of four
    /// hundred characters, an address and a number inside an Arabic line, a very long title.
    /// </summary>
    public static EmailContent Lengthy(string language)
    {
        var link = $"{App}/password/reset#token={new string('a', 180)}-{new string('B', 180)}";
        const string Clinic = "Al Noor Specialised Centre for Dentistry, Orthodontics, Implants and Cosmetic Dental Surgery — Heliopolis Main Branch, Cairo";
        const string ClinicAr = "مركز النور التخصصي لطب وجراحة الفم والأسنان وتقويم الأسنان وزراعتها والتجميل — الفرع الرئيسي بمصر الجديدة، القاهرة";

        return language == "en"
            ? new(To, "en", $"Invitation to {Clinic}", "INVITATION",
                $"An account was created for you at {Clinic}, and its title is long enough to need several lines",
                [
                    new EmailParagraph($"Sent to {EmailContent.LeftToRight("a.very.long.address.of.someone@a-rather-long-domain-name.example.com")} about invoice {EmailContent.LeftToRight("INV-2026-0068")}."),
                    new EmailFacts([new EmailFact("A name that is itself rather long for a row", "a.very.long.address.of.someone@a-rather-long-domain-name.example.com")]),
                    new EmailButton("Set password and sign in to the clinic", link),
                    new EmailNote("Weren’t expecting this? You can ignore this email."),
                ])
            : new(To, "ar", $"دعوة إلى {ClinicAr}", "دعوة",
                $"أُنشئ لك حساب في {ClinicAr}، وعنوانه طويل يحتاج إلى عدة أسطر",
                [
                    new EmailParagraph($"أُرسلت إلى {EmailContent.LeftToRight("a.very.long.address.of.someone@a-rather-long-domain-name.example.com")} بخصوص الفاتورة {EmailContent.LeftToRight("INV-2026-0068")} بتاريخ 25 أكتوبر 2026."),
                    new EmailFacts([new EmailFact("اسم طويل نسبيًّا لصف واحد من الوقائع", EmailContent.LeftToRight("a.very.long.address.of.someone@a-rather-long-domain-name.example.com"))]),
                    new EmailButton("تعيين كلمة المرور وتسجيل الدخول إلى العيادة", link),
                    new EmailNote("لم تكن تتوقع هذه الرسالة؟ يمكنك تجاهلها."),
                ]);
    }
}
