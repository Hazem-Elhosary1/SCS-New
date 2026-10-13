using System.Globalization;
using Scs.BuildingBlocks.Mail;
using Scs.Operators.Domain;
using Scs.SharedKernel.Wording;

namespace Scs.Operators.Infrastructure;

/// <summary>
/// What the emails of the operators console say (spec 010, FR-055; spec 094): each in the
/// account's language only, with its code or link once. Only the words are here — the layout,
/// the footer and the platform's name are the template's. They are sent at once through the
/// platform's mail sender: a code that waits in a queue is a sign-in that waits. The words are
/// in specs/094-email-template/contracts/emails.md §C.
/// </summary>
internal static class OperatorEmails
{
    private const string Platform = EmailContent.Platform;

    /// <summary>The code, how long it lasts, and that nobody will ask for it. No link (FR-024).</summary>
    public static EmailContent Code(string to, string language, string code, int validMinutes, string purpose)
    {
        var minutes = validMinutes.ToString(CultureInfo.InvariantCulture);
        var changing = purpose == ChallengePurposes.PasswordChange;

        return language == Words.English
            ? new(
                to, "en",
                changing ? "Code to confirm your password change" : "Your verification code",
                "VERIFICATION CODE", "Confirm it’s you",
                [
                    new EmailParagraph(changing
                        ? "Use this code to confirm the change of your Ops Console password."
                        : $"Use this code to finish signing in to {Platform}."),
                    new EmailCode(code),
                    new EmailNote($"This code expires in {minutes} minutes."),
                    new EmailNote(changing
                        ? "Didn’t ask to change your password? Don’t enter the code, and change your password now."
                        : "Didn’t try to sign in? Someone may know your password — change it now."),
                    new EmailNote($"Nobody from {Platform} will ask you for this code."),
                ])
            : new(
                to, "ar",
                changing ? "رمز تأكيد تغيير كلمة المرور" : "رمز التحقق",
                "رمز التحقق", "تأكيد هويتك",
                [
                    new EmailParagraph(changing
                        ? "استخدم هذا الرمز لتأكيد تغيير كلمة مرورك في لوحة التشغيل."
                        : $"استخدم هذا الرمز لإكمال تسجيل الدخول إلى {Platform}."),
                    new EmailCode(code),
                    new EmailNote($"ينتهي هذا الرمز خلال {minutes} دقائق."),
                    new EmailNote(changing
                        ? "لم تطلب تغيير كلمة المرور؟ لا تُدخل الرمز، وغيّر كلمة مرورك الآن."
                        : "لم تحاول تسجيل الدخول؟ قد يكون شخص ما يعرف كلمة مرورك — غيّرها الآن."),
                    new EmailNote($"لن يطلب منك أحد من {Platform} هذا الرمز."),
                ]);
    }

    public static EmailContent ResetLink(string to, string language, string link, int validMinutes)
    {
        var minutes = validMinutes.ToString(CultureInfo.InvariantCulture);
        return language == Words.English
            ? new(
                to, "en", "Reset your password", "PASSWORD RESET", "Choose a new password",
                [
                    new EmailParagraph($"We received a request to reset the password for your {Platform} account. The link works once and expires in {minutes} minutes."),
                    new EmailButton("Reset password", link),
                    new EmailNote("Didn’t ask for this? Ignore this email — your password stays the same."),
                ])
            : new(
                to, "ar", "إعادة تعيين كلمة المرور", "إعادة تعيين كلمة المرور", "اختر كلمة مرور جديدة",
                [
                    new EmailParagraph($"وصلنا طلب لإعادة تعيين كلمة مرور حسابك في {Platform}. يعمل الرابط مرة واحدة وينتهي خلال {minutes} دقيقة."),
                    new EmailButton("إعادة تعيين كلمة المرور", link),
                    new EmailNote("لم تطلب ذلك؟ تجاهل هذه الرسالة — ستبقى كلمة مرورك كما هي."),
                ]);
    }

    public static EmailContent FirstPassword(string to, string language, string link, int validHours)
    {
        var hours = validHours.ToString(CultureInfo.InvariantCulture);
        return language == Words.English
            ? new(
                to, "en", "Invitation to the Ops Console", "INVITATION", "Join the Ops Console",
                [
                    new EmailParagraph($"You were added to the {Platform} Ops Console. Set your password to sign in — the link expires in {hours} hours."),
                    new EmailButton("Set password", link),
                    new EmailNote("Weren’t expecting this? You can ignore this email."),
                ],
                "You’re receiving this email because an administrator added you to the Ops Console.")
            : new(
                to, "ar", "دعوة إلى لوحة التشغيل", "دعوة", "انضم إلى لوحة التشغيل",
                [
                    new EmailParagraph($"أُضفت إلى لوحة التشغيل الخاصة بـ{Platform}. عيّن كلمة المرور لتسجيل الدخول — ينتهي الرابط خلال {hours} ساعة."),
                    new EmailButton("تعيين كلمة المرور", link),
                    new EmailNote("لم تكن تتوقع هذه الرسالة؟ يمكنك تجاهلها."),
                ],
                "وصلتك هذه الرسالة لأن أحد المسؤولين أضافك إلى لوحة التشغيل.");
    }

    /// <summary>A sign-in from a browser the account never used (FR-033a): what, where from, when.</summary>
    public static EmailContent NewBrowser(string to, string language, string? userAgent, string? address, DateTimeOffset when)
    {
        var english = language == Words.English;
        var device = DeviceText.Describe(userAgent, english ? Words.English : Words.Arabic);
        var moment = Words.Western(Words.Moment(when, english));

        var facts = new List<EmailFact> { new(english ? "Browser" : "المتصفح", device) };
        if (!string.IsNullOrWhiteSpace(address))
        {
            facts.Add(new(english ? "Address" : "العنوان", EmailContent.LeftToRight(address)));
        }

        facts.Add(new(english ? "When" : "الوقت", moment));

        return english
            ? new(
                to, "en", "New sign-in to your account", "ACCOUNT SECURITY", "New sign-in to your account",
                [
                    new EmailParagraph("Your Ops Console account was signed in to from a browser we have not seen before."),
                    new EmailFacts(facts),
                    new EmailNote("If this was you, nothing needs doing. If it was not, reset your password now from the sign-in screen: that ends every session."),
                ])
            : new(
                to, "ar", "تسجيل دخول من متصفح جديد", "أمان الحساب", "تسجيل دخول من متصفح جديد",
                [
                    new EmailParagraph("سُجّل الدخول إلى حسابك في لوحة التشغيل من متصفح لم نره من قبل."),
                    new EmailFacts(facts),
                    new EmailNote("إن كنت أنت، فلا شيء مطلوب. وإن لم تكن، أعد تعيين كلمة مرورك الآن من شاشة تسجيل الدخول: ذلك يُنهي كل الجلسات."),
                ]);
    }
}
