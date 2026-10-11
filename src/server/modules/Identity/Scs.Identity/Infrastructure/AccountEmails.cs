using System.Globalization;
using System.Net;
using Scs.BuildingBlocks.Mail;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// The emails about a person's password and account (spec 004, FR-017, FR-032, FR-043): Arabic
/// then English, like the sign-in code. None contains a password, and none says anything about
/// the clinic beyond its name on the two messages that welcome someone to it. Nobody can switch
/// them off. Templates and the clinic's language arrive with the notifications engine (spec
/// 008); the wording of the three the design file does not draw is fixed at the page review.
/// </summary>
internal static class AccountEmails
{
    private const string Product = "Smart Clinic OS";

    /// <summary>The action has one name wherever it appears (CL-SH-097).</summary>
    public const string ResetSubject = "إعادة تعيين كلمة المرور — Reset your password";

    public const string ChangedSubject = "غُيّرت كلمة المرور — Your password was changed";
    public const string InvitationSubject = "حسابك الجديد — Your new account";
    public const string CreatedSubject = "أُنشئ حساب لك — An account was created for you";
    public const string AccessResetSubject = "أُعيد ضبط الدخول إلى حسابك — Access to your account was reset";

    public static EmailMessage ResetLink(string to, string address, int validMinutes)
    {
        var minutes = validMinutes.ToString(CultureInfo.InvariantCulture);
        return Build(
            to,
            ResetSubject,
            address,
            [
                $"وصلنا طلب إعادة تعيين كلمة المرور لهذا الحساب في {Product}. لاختيار كلمة مرور جديدة يُفتح الرابط التالي:",
                $"الرابط صالح لمدة {minutes} دقيقة ويعمل مرة واحدة. من لم يطلب ذلك فليتجاهل هذه الرسالة؛ كلمة المرور الحالية تبقى كما هي.",
            ],
            [
                $"A password reset was requested for this account on {Product}. To choose a new password, open this link:",
                $"The link is valid for {minutes} minutes and works once. Anyone who did not ask for it can ignore this message; the current password stays as it is.",
            ]);
    }

    public static EmailMessage PasswordChanged(string to, DateTimeOffset when)
    {
        var at = when.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return Build(
            to,
            ChangedSubject,
            address: null,
            [
                $"غُيّرت كلمة مرور هذا الحساب في {Product} بتاريخ {at} (التوقيت العالمي).",
                "إن لم يكن هذا التغيير من صاحب الحساب فيُرجى التواصل مع فريق الدعم فورًا.",
            ],
            [
                $"The password of this account on {Product} was changed on {at} (UTC).",
                "If the account's owner did not make this change, contact support at once.",
            ]);
    }

    public static EmailMessage Invitation(
        string to, string address, string clinicAr, string clinicEn, string signInAddress, int validHours)
    {
        var hours = validHours.ToString(CultureInfo.InvariantCulture);
        return Build(
            to,
            InvitationSubject,
            address,
            [
                $"أُنشئ حساب بهذا البريد في {clinicAr} على {Product}. لاختيار كلمة المرور وتفعيل الحساب يُفتح الرابط التالي:",
                $"الرابط صالح {hours} ساعة من إرساله ويعمل مرة واحدة. بعد التفعيل يكون الدخول من: {signInAddress}",
            ],
            [
                $"An account was created with this email at {clinicEn} on {Product}. To choose a password and activate the account, open this link:",
                $"The link is valid for {hours} hours from sending and works once. After activation, sign in at: {signInAddress}",
            ]);
    }

    public static EmailMessage AccountCreated(string to, string clinicAr, string clinicEn, string signInAddress) => Build(
        to,
        CreatedSubject,
        address: null,
        [
            $"أُنشئ حساب بهذا البريد في {clinicAr} على {Product}. الدخول من: {signInAddress}",
            "كلمة المرور يسلّمها من أنشأ الحساب، ولا تُرسل بالبريد. عند أول دخول يُطلب اختيار كلمة مرور جديدة.",
        ],
        [
            $"An account was created with this email at {clinicEn} on {Product}. Sign in at: {signInAddress}",
            "The password is handed over by whoever created the account; it is never sent by email. At first sign-in a new password must be chosen.",
        ]);

    public static EmailMessage AccessReset(string to, string signInAddress) => Build(
        to,
        AccessResetSubject,
        address: null,
        [
            $"أعاد أحد أعضاء فريق العيادة ضبط الدخول إلى هذا الحساب في {Product}، وكلمة المرور السابقة لم تعد تعمل.",
            $"كلمة المرور المؤقتة يسلّمها من أعاد الضبط، ولا تُرسل بالبريد. الدخول من: {signInAddress} — وعند أول دخول يُطلب اختيار كلمة مرور جديدة.",
            "إن كان هذا غير متوقَّع فيُرجى التواصل مع مسؤول العيادة أو فريق الدعم.",
        ],
        [
            $"Someone on the clinic's team reset access to this account on {Product}; the earlier password no longer works.",
            $"The temporary password is handed over by whoever reset it; it is never sent by email. Sign in at: {signInAddress} — at first sign-in a new password must be chosen.",
            "If this was not expected, contact the clinic's manager or support.",
        ]);

    /// <summary>The link, when there is one, goes after the first paragraph of each language.</summary>
    private static EmailMessage Build(string to, string subject, string? address, string[] arabic, string[] english)
    {
        var text = string.Join(
            "\n\n",
            [.. WithAddress(arabic, address), "----", .. WithAddress(english, address)]);

        var html =
            $"""
             <!doctype html>
             <html>
             <body style="font-family: Arial, sans-serif; color: #1f2937;">
               <div dir="rtl" lang="ar">
             {Paragraphs(arabic, address)}
               </div>
               <hr>
               <div dir="ltr" lang="en">
             {Paragraphs(english, address)}
               </div>
             </body>
             </html>
             """;

        return new EmailMessage(to, subject, text, html);
    }

    private static IEnumerable<string> WithAddress(string[] paragraphs, string? address)
    {
        for (var i = 0; i < paragraphs.Length; i++)
        {
            yield return paragraphs[i];
            if (i == 0 && address is not null)
            {
                yield return address;
            }
        }
    }

    private static string Paragraphs(string[] paragraphs, string? address)
    {
        var lines = new List<string>();
        for (var i = 0; i < paragraphs.Length; i++)
        {
            lines.Add($"    <p>{WebUtility.HtmlEncode(paragraphs[i])}</p>");
            if (i == 0 && address is not null)
            {
                var safe = WebUtility.HtmlEncode(address);
                lines.Add($"    <p dir=\"ltr\"><a href=\"{safe}\">{safe}</a></p>");
            }
        }

        return string.Join('\n', lines);
    }
}
