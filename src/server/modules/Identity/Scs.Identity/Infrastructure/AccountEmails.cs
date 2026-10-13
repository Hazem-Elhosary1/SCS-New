using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Scs.BuildingBlocks.Mail;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel.Wording;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// What the emails about a person's sign-in, password and account say (spec 003, FR-055; spec
/// 004, FR-017, FR-032, FR-043; spec 094): each in the recipient's language only. Only the
/// words are here — the layout, the footer and the platform's name are the template's. None
/// contains a password, and none says anything about the clinic beyond its name on the
/// messages that welcome someone to it. Nobody can switch them off. The words are in
/// specs/094-email-template/contracts/emails.md §A.
/// </summary>
internal static class AccountEmails
{
    private const string Platform = EmailContent.Platform;

    /// <summary>The code after a correct password, and how long it lasts. No link.</summary>
    public static EmailContent SignInCode(string to, string language, string code, int validMinutes)
    {
        var minutes = validMinutes.ToString(CultureInfo.InvariantCulture);
        return language == Words.English
            ? new(
                to, "en", "Your verification code", "VERIFICATION CODE", "Confirm it’s you",
                [
                    new EmailParagraph($"Use this code to finish signing in to {Platform}."),
                    new EmailCode(code),
                    new EmailNote($"This code expires in {minutes} minutes."),
                    new EmailNote("Didn’t try to sign in? Someone may know your password — change it now."),
                    new EmailNote($"Nobody from {Platform} will ask you for this code."),
                ])
            : new(
                to, "ar", "رمز التحقق", "رمز التحقق", "تأكيد هويتك",
                [
                    new EmailParagraph($"استخدم هذا الرمز لإكمال تسجيل الدخول إلى {Platform}."),
                    new EmailCode(code),
                    new EmailNote($"ينتهي هذا الرمز خلال {minutes} دقائق."),
                    new EmailNote("لم تحاول تسجيل الدخول؟ قد يكون شخص ما يعرف كلمة مرورك — غيّرها الآن."),
                    new EmailNote($"لن يطلب منك أحد من {Platform} هذا الرمز."),
                ]);
    }

    /// <summary>The action has one name wherever it appears (CL-SH-097).</summary>
    public static EmailContent PasswordReset(string to, string language, string address, int validMinutes)
    {
        var minutes = validMinutes.ToString(CultureInfo.InvariantCulture);
        return language == Words.English
            ? new(
                to, "en", "Reset your password", "PASSWORD RESET", "Choose a new password",
                [
                    new EmailParagraph($"We received a request to reset the password for your {Platform} account. The link works once and expires in {minutes} minutes."),
                    new EmailButton("Reset password", address),
                    new EmailNote("Didn’t ask for this? Ignore this email — your password stays the same."),
                ])
            : new(
                to, "ar", "إعادة تعيين كلمة المرور", "إعادة تعيين كلمة المرور", "اختر كلمة مرور جديدة",
                [
                    new EmailParagraph($"وصلنا طلب لإعادة تعيين كلمة مرور حسابك في {Platform}. يعمل الرابط مرة واحدة وينتهي خلال {minutes} دقيقة."),
                    new EmailButton("إعادة تعيين كلمة المرور", address),
                    new EmailNote("لم تطلب ذلك؟ تجاهل هذه الرسالة — ستبقى كلمة مرورك كما هي."),
                ]);
    }

    /// <summary>The time is Cairo's, as the consoles write it.</summary>
    public static EmailContent PasswordChanged(string to, string language, DateTimeOffset when)
    {
        var english = language == Words.English;
        var at = Words.Western(Words.Moment(when, english));
        return english
            ? new(
                to, "en", "Your password was changed", "ACCOUNT SECURITY", "Your password was changed",
                [
                    new EmailParagraph($"The password of your {Platform} account was changed."),
                    new EmailFacts([new EmailFact("When", at)]),
                    new EmailNote("If you did not make this change, contact support at once."),
                ])
            : new(
                to, "ar", "تم تغيير كلمة مرورك", "أمان الحساب", "تم تغيير كلمة مرورك",
                [
                    new EmailParagraph($"غُيّرت كلمة مرور حسابك في {Platform}."),
                    new EmailFacts([new EmailFact("الوقت", at)]),
                    new EmailNote("إن لم تكن أنت من غيّرها، تواصل مع فريق الدعم فورًا."),
                ]);
    }

    /// <summary>A team member is added and chooses the password through a link.</summary>
    public static EmailContent Invitation(
        string to, string language, string address, string clinicAr, string clinicEn, int validHours)
    {
        var hours = validHours.ToString(CultureInfo.InvariantCulture);
        var english = language == Words.English;
        var clinic = ClinicName(english, clinicAr, clinicEn);
        return english
            ? new(
                to, "en", $"Invitation to {clinic}", "INVITATION", $"Welcome to {Platform}",
                [
                    new EmailParagraph($"An account was created for you at {clinic} on {Platform}. Set your password to sign in — the link expires in {hours} hours."),
                    new EmailButton("Set password", address),
                    new EmailNote("Weren’t expecting this? You can ignore this email."),
                ],
                CreatedReason(english, clinic))
            : new(
                to, "ar", $"دعوة إلى {clinic}", "دعوة", $"مرحبًا بك في {Platform}",
                [
                    new EmailParagraph($"أُنشئ لك حساب في {clinic} على {Platform}. عيّن كلمة المرور لتسجيل الدخول — ينتهي الرابط خلال {hours} ساعة."),
                    new EmailButton("تعيين كلمة المرور", address),
                    new EmailNote("لم تكن تتوقع هذه الرسالة؟ يمكنك تجاهلها."),
                ],
                CreatedReason(english, clinic));
    }

    /// <summary>An account whose password is handed over in person; the email holds none.</summary>
    public static EmailContent AccountCreated(
        string to, string language, string clinicAr, string clinicEn, string signInAddress)
    {
        var english = language == Words.English;
        var clinic = ClinicName(english, clinicAr, clinicEn);
        return english
            ? new(
                to, "en", $"An account was created for you at {clinic}", "NEW ACCOUNT", $"Welcome to {Platform}",
                [
                    new EmailParagraph($"An account was created for you at {clinic} on {Platform}."),
                    new EmailParagraph("The password is handed to you by whoever created the account; it is never sent by email. At first sign-in you choose a new one."),
                    new EmailButton("Sign in", signInAddress),
                ],
                CreatedReason(english, clinic))
            : new(
                to, "ar", $"أُنشئ حساب لك في {clinic}", "حساب جديد", $"مرحبًا بك في {Platform}",
                [
                    new EmailParagraph($"أُنشئ لك حساب في {clinic} على {Platform}."),
                    new EmailParagraph("كلمة المرور يسلّمها لك من أنشأ الحساب، ولا تُرسل بالبريد. عند أول دخول يُطلب منك اختيار كلمة مرور جديدة."),
                    new EmailButton("تسجيل الدخول", signInAddress),
                ],
                CreatedReason(english, clinic));
    }

    /// <summary>Someone at the clinic reset a team member's access; the email holds no password.</summary>
    public static EmailContent AccessReset(string to, string language, string signInAddress) => language == Words.English
        ? new(
            to, "en", "Access to your account was reset", "ACCOUNT SECURITY", "Access to your account was reset",
            [
                new EmailParagraph($"Someone on the clinic’s team reset access to your {Platform} account; the earlier password no longer works."),
                new EmailParagraph("The temporary password is handed to you by whoever reset it; it is never sent by email. At first sign-in you choose a new one."),
                new EmailButton("Sign in", signInAddress),
                new EmailNote("If this was not expected, contact the clinic’s manager or support."),
            ])
        : new(
            to, "ar", "أُعيد ضبط الدخول إلى حسابك", "أمان الحساب", "أُعيد ضبط الدخول إلى حسابك",
            [
                new EmailParagraph($"أعاد أحد أعضاء فريق العيادة ضبط الدخول إلى حسابك في {Platform}، وكلمة المرور السابقة لم تعد تعمل."),
                new EmailParagraph("كلمة المرور المؤقتة يسلّمها لك من أعاد الضبط، ولا تُرسل بالبريد. عند أول دخول يُطلب منك اختيار كلمة مرور جديدة."),
                new EmailButton("تسجيل الدخول", signInAddress),
                new EmailNote("إن لم يكن هذا متوقَّعًا، تواصل مع مسؤول العيادة أو فريق الدعم."),
            ]);

    /// <summary>The clinic's name in the email's language; the other when that one is not kept.</summary>
    private static string ClinicName(bool english, string clinicAr, string clinicEn)
    {
        var (first, second) = english ? (clinicEn, clinicAr) : (clinicAr, clinicEn);
        return string.IsNullOrWhiteSpace(first) ? second : first;
    }

    private static string CreatedReason(bool english, string clinic) => english
        ? $"You’re receiving this email because an account was created for you at {clinic} on {Platform}."
        : $"وصلتك هذه الرسالة لأنه أُنشئ لك حساب في {clinic} على {Platform}.";
}

/// <summary>
/// The language an account's emails are written in (spec 094, FR-007): the one the person
/// kept; Arabic for someone who has kept none yet, as for a new account.
/// </summary>
internal static class AccountLanguage
{
    /// <summary>Reads within the established clinic, like everything about an account.</summary>
    public static async Task<string> OfAsync(IdentityDbContext db, Guid accountId, CancellationToken cancellation) =>
        Words.Language(
            await db.Preferences.AsNoTracking()
                .Where(p => p.AccountId == accountId)
                .Select(p => p.Language)
                .FirstOrDefaultAsync(cancellation)
                .ConfigureAwait(false));
}
