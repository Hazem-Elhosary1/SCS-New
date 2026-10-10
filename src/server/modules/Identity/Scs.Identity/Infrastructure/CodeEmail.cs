using System.Globalization;
using System.Net;
using Scs.BuildingBlocks.Mail;

namespace Scs.Identity.Infrastructure;

/// <summary>
/// The one email of sign-in: the code and how long it lasts, in Arabic then English, and
/// nothing about the clinic or its data (spec 003, FR-055). Templates and the clinic's language
/// arrive with the notifications engine (spec 008).
/// </summary>
internal static class CodeEmail
{
    public const string Subject = "رمز الدخول — Sign-in code";

    public static EmailMessage Build(string to, string code, int validMinutes)
    {
        var minutes = validMinutes.ToString(CultureInfo.InvariantCulture);

        var text =
            $"""
             رمز الدخول إلى Smart Clinic OS:

             {code}

             الرمز صالح لمدة {minutes} دقائق، ويُستعمل مرة واحدة. من لم يطلب هذا الرمز فليتجاهل هذه الرسالة.

             ----

             Sign-in code for Smart Clinic OS:

             {code}

             The code is valid for {minutes} minutes and works once. Anyone who did not ask for it can ignore this message.
             """;

        var safe = WebUtility.HtmlEncode(code);
        var html =
            $"""
             <!doctype html>
             <html>
             <body style="font-family: Arial, sans-serif; color: #1f2937;">
               <div dir="rtl" lang="ar">
                 <p>رمز الدخول إلى Smart Clinic OS:</p>
                 <p dir="ltr" style="font-size: 28px; font-weight: bold; letter-spacing: 6px;">{safe}</p>
                 <p>الرمز صالح لمدة {minutes} دقائق، ويُستعمل مرة واحدة. من لم يطلب هذا الرمز فليتجاهل هذه الرسالة.</p>
               </div>
               <hr>
               <div dir="ltr" lang="en">
                 <p>Sign-in code for Smart Clinic OS:</p>
                 <p style="font-size: 28px; font-weight: bold; letter-spacing: 6px;">{safe}</p>
                 <p>The code is valid for {minutes} minutes and works once. Anyone who did not ask for it can ignore this message.</p>
               </div>
             </body>
             </html>
             """;

        return new EmailMessage(to, Subject, text, html);
    }
}
