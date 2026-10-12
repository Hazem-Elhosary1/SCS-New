using System.Globalization;
using System.Net;
using Scs.BuildingBlocks.Mail;
using Scs.Operators.Domain;
using Scs.SharedKernel.Wording;

namespace Scs.Operators.Infrastructure;

/// <summary>
/// The emails of the operators console (spec 010, FR-055): each in the account's language
/// only, with the platform's name in its subject, and its code or link once. They are sent at
/// once through the platform's mail sender: a code that waits in a queue is a sign-in that
/// waits. The wording is the owner's to confirm (contracts/operator-screens.md §4).
/// </summary>
internal static class OperatorEmails
{
    private const string Platform = "Smart Clinic OS";

    /// <summary>The code, how long it lasts, and that nobody will ask for it. No link (FR-024).</summary>
    public static EmailMessage Code(string to, string language, string code, int validMinutes, string purpose)
    {
        var minutes = validMinutes.ToString(CultureInfo.InvariantCulture);
        var changing = purpose == ChallengePurposes.PasswordChange;

        return language == "en"
            ? Build(
                to, "en",
                changing ? $"Code to change your password — {Platform}" : $"Sign-in code — {Platform}",
                changing
                    ? "Use this code to confirm the change of your operations console password:"
                    : "Use this code to sign in to the operations console:",
                code,
                $"The code is valid for {minutes} minutes and works once. Nobody from {Platform} will ask you for it. If you did not ask for it, ignore this message and change your password.")
            : Build(
                to, "ar",
                changing ? $"رمز تغيير كلمة المرور — {Platform}" : $"رمز الدخول — {Platform}",
                changing
                    ? "يُستعمل هذا الرمز لتأكيد تغيير كلمة مرور لوحة التشغيل:"
                    : "يُستعمل هذا الرمز للدخول إلى لوحة التشغيل:",
                code,
                $"الرمز صالح لمدة {minutes} دقائق، ويُستعمل مرة واحدة. لن يطلبه أحد من {Platform}. من لم يطلب هذا الرمز فليتجاهل الرسالة ويغيّر كلمة المرور.");
    }

    public static EmailMessage ResetLink(string to, string language, string link, int validMinutes)
    {
        var minutes = validMinutes.ToString(CultureInfo.InvariantCulture);
        return language == "en"
            ? Build(
                to, "en", $"Reset your password — {Platform}",
                "Open this link to choose a new password for the operations console:", link,
                $"The link is valid for {minutes} minutes and works once. If you did not ask for it, ignore this message: your password stays as it is.")
            : Build(
                to, "ar", $"إعادة تعيين كلمة المرور — {Platform}",
                "يُفتح هذا الرابط لاختيار كلمة مرور جديدة للوحة التشغيل:", link,
                $"الرابط صالح لمدة {minutes} دقيقة، ويُستعمل مرة واحدة. من لم يطلبه فليتجاهل الرسالة: تبقى كلمة المرور كما هي.");
    }

    public static EmailMessage FirstPassword(string to, string language, string link, int validHours)
    {
        var hours = validHours.ToString(CultureInfo.InvariantCulture);
        return language == "en"
            ? Build(
                to, "en", $"Join the operations console — {Platform}",
                $"An operator account was created for you on {Platform}. Open this link to choose your password and activate it:", link,
                $"The link is valid for {hours} hours and works once.")
            : Build(
                to, "ar", $"انضم إلى لوحة التشغيل — {Platform}",
                $"أُنشئ لك حساب مشغّل على {Platform}. يُفتح هذا الرابط لاختيار كلمة المرور وتفعيل الحساب:", link,
                $"الرابط صالح لمدة {hours} ساعة، ويُستعمل مرة واحدة.");
    }

    /// <summary>A sign-in from a browser the account never used (FR-033a): what, where from, when.</summary>
    public static EmailMessage NewBrowser(string to, string language, string? userAgent, string? address, DateTimeOffset when)
    {
        var device = DeviceText.Describe(userAgent, language);
        var from = string.IsNullOrEmpty(address) ? "—" : address;
        var moment = TimeZoneInfo.ConvertTime(when, Cairo).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        return language == "en"
            ? Build(
                to, "en", $"New sign-in to the operations console — {Platform}",
                "Your operator account was signed in to from a browser we have not seen before:",
                $"{device} · {from} · {moment} (Cairo)",
                "If this was you, nothing needs doing. If it was not, reset your password now from the sign-in screen: that ends every session.")
            : Build(
                to, "ar", $"دخول جديد إلى لوحة التشغيل — {Platform}",
                "سُجّل الدخول إلى حساب المشغّل الخاص بك من متصفّح لم نره من قبل:",
                $"{device} · {from} · {moment} (القاهرة)",
                "إن كان هذا أنت فلا شيء مطلوب. وإن لم يكن، تُعاد تعيين كلمة المرور الآن من شاشة الدخول: ذلك يُنهي كل الجلسات.");
    }

    private static TimeZoneInfo Cairo { get; } = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    private static EmailMessage Build(string to, string language, string subject, string lead, string value, string after)
    {
        var text =
            $"""
             {lead}

             {value}

             {after}
             """;

        var direction = language == "ar" ? "rtl" : "ltr";
        var html =
            $"""
             <!doctype html>
             <html>
             <body style="font-family: Arial, sans-serif; color: #1f2937;">
               <div dir="{direction}" lang="{language}">
                 <p>{WebUtility.HtmlEncode(lead)}</p>
                 <p dir="ltr" style="font-size: 20px; font-weight: bold; word-break: break-all;">{WebUtility.HtmlEncode(value)}</p>
                 <p>{WebUtility.HtmlEncode(after)}</p>
               </div>
             </body>
             </html>
             """;

        return new EmailMessage(to, subject, text, html);
    }
}
