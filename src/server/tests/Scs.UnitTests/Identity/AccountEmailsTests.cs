using System.Text.RegularExpressions;
using Scs.BuildingBlocks.Mail;
using Scs.Identity.Infrastructure;
using Scs.UnitTests.Mail;

namespace Scs.UnitTests.Identity;

/// <summary>
/// Spec 094, FR-026 to FR-030: the clinic team's account emails say the words of
/// contracts/emails.md §A, each in one language, with no password anywhere.
/// </summary>
public partial class AccountEmailsTests
{
    private const string To = "sara@noor.example";
    private const string Link = "https://app.scs.test/set-password#t=v1.token";
    private const string SignIn = "https://app.scs.test/sign-in";
    private const string NameAr = "سمارت كلينك أو إس";
    private const string NameEn = "Smart Clinic OS";

    private static readonly DateTimeOffset When = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [GeneratedRegex(@"\p{IsArabic}")]
    private static partial Regex ArabicLetter();

    private static EmailMessage Render(EmailContent content) => EmailRenderer.Render(content, EmailRendererTests.Full);

    private static EmailButton Button(EmailContent content) => content.Parts.OfType<EmailButton>().Single();

    /// <summary>One language only: an English email has no Arabic letter; an Arabic one no English sentence.</summary>
    private static void InOneLanguage(EmailMessage message, string language, params string[] latinAllowed)
    {
        if (language == "en")
        {
            Assert.DoesNotMatch(ArabicLetter(), message.Subject + message.TextBody.Replace("عيادة النور", "", StringComparison.Ordinal));
            Assert.Equal(NameEn, message.SenderName);
        }
        else
        {
            var text = latinAllowed.Aggregate(message.Subject + message.TextBody, (all, part) => all.Replace(part, "", StringComparison.Ordinal));
            Assert.DoesNotMatch("[A-Za-z]{2,} [A-Za-z]{2,}", text);
            Assert.Equal(NameAr, message.SenderName);
        }

        Assert.DoesNotContain(NameEn, message.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain(NameAr, message.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("[", message.Subject, StringComparison.Ordinal);
        Assert.DoesNotMatch("[٠-٩]", message.Subject + message.TextBody + message.HtmlBody);
    }

    [Theory]
    [InlineData("ar", "رمز التحقق", "رمز التحقق", "تأكيد هويتك", "استخدم هذا الرمز لإكمال تسجيل الدخول إلى سمارت كلينك أو إس.", "ينتهي هذا الرمز خلال 10 دقائق.", "لم تحاول تسجيل الدخول؟ قد يكون شخص ما يعرف كلمة مرورك — غيّرها الآن.", "لن يطلب منك أحد من سمارت كلينك أو إس هذا الرمز.")]
    [InlineData("en", "Your verification code", "VERIFICATION CODE", "Confirm it’s you", "Use this code to finish signing in to Smart Clinic OS.", "This code expires in 10 minutes.", "Didn’t try to sign in? Someone may know your password — change it now.", "Nobody from Smart Clinic OS will ask you for this code.")]
    public void The_sign_in_code_holds_its_code_once_three_notes_and_no_link(
        string language, string subject, string eyebrow, string title, string body, string life, string warning, string nobody)
    {
        var content = AccountEmails.SignInCode(To, language, "427913", 10);
        var message = Render(content);

        Assert.Equal((To, subject, eyebrow, title), (message.To, content.Subject, content.Eyebrow, content.Title));
        Assert.Equal([body], content.Parts.OfType<EmailParagraph>().Select(p => p.Text.Replace(EmailContent.Platform, language == "ar" ? NameAr : NameEn, StringComparison.Ordinal)));
        Assert.Contains(life, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(warning, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(nobody, message.TextBody, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(message.TextBody, "427913"));
        Assert.Empty(content.Parts.OfType<EmailButton>());
        Assert.DoesNotContain("http", message.TextBody, StringComparison.OrdinalIgnoreCase);
        InOneLanguage(message, language);
    }

    [Theory]
    [InlineData("ar", "إعادة تعيين كلمة المرور", "إعادة تعيين كلمة المرور", "اختر كلمة مرور جديدة", "وصلنا طلب لإعادة تعيين كلمة مرور حسابك في سمارت كلينك أو إس. يعمل الرابط مرة واحدة وينتهي خلال 30 دقيقة.", "إعادة تعيين كلمة المرور", "لم تطلب ذلك؟ تجاهل هذه الرسالة — ستبقى كلمة مرورك كما هي.")]
    [InlineData("en", "Reset your password", "PASSWORD RESET", "Choose a new password", "We received a request to reset the password for your Smart Clinic OS account. The link works once and expires in 30 minutes.", "Reset password", "Didn’t ask for this? Ignore this email — your password stays the same.")]
    public void The_reset_email_holds_one_button_with_its_link(
        string language, string subject, string eyebrow, string title, string body, string button, string note)
    {
        var content = AccountEmails.PasswordReset(To, language, Link, 30);
        var message = Render(content);

        Assert.Equal((subject, eyebrow, title), (content.Subject, content.Eyebrow, content.Title));
        Assert.Contains(body, message.TextBody, StringComparison.Ordinal);
        Assert.Equal((button, Link), (Button(content).Label, Button(content).Address));
        Assert.Contains(note, message.TextBody, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(message.TextBody, "https://"));
        InOneLanguage(message, language, Link);
    }

    [Theory]
    [InlineData("ar", "تم تغيير كلمة مرورك", "أمان الحساب", "غُيّرت كلمة مرور حسابك في سمارت كلينك أو إس.", "الوقت: 12 أكتوبر 2026، 12:00 م", "إن لم تكن أنت من غيّرها، تواصل مع فريق الدعم فورًا.")]
    [InlineData("en", "Your password was changed", "ACCOUNT SECURITY", "The password of your Smart Clinic OS account was changed.", "When: 12 October 2026, 12:00 PM", "If you did not make this change, contact support at once.")]
    public void The_password_changed_email_gives_its_time_in_cairo_time_and_has_no_button(
        string language, string subject, string eyebrow, string body, string when, string note)
    {
        var content = AccountEmails.PasswordChanged(To, language, When);
        var message = Render(content);

        Assert.Equal((subject, eyebrow, subject), (content.Subject, content.Eyebrow, content.Title));
        Assert.Contains(body, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(when, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(note, message.TextBody, StringComparison.Ordinal);
        Assert.Empty(content.Parts.OfType<EmailButton>());
        InOneLanguage(message, language);
    }

    [Theory]
    [InlineData("ar", "دعوة إلى عيادة النور", "دعوة", "مرحبًا بك في سمارت كلينك أو إس", "أُنشئ لك حساب في عيادة النور على سمارت كلينك أو إس. عيّن كلمة المرور لتسجيل الدخول — ينتهي الرابط خلال 72 ساعة.", "تعيين كلمة المرور", "لم تكن تتوقع هذه الرسالة؟ يمكنك تجاهلها.", "وصلتك هذه الرسالة لأنه أُنشئ لك حساب في عيادة النور على سمارت كلينك أو إس.")]
    [InlineData("en", "Invitation to Al Noor Clinic", "INVITATION", "Welcome to Smart Clinic OS", "An account was created for you at Al Noor Clinic on Smart Clinic OS. Set your password to sign in — the link expires in 72 hours.", "Set password", "Weren’t expecting this? You can ignore this email.", "You’re receiving this email because an account was created for you at Al Noor Clinic on Smart Clinic OS.")]
    public void The_invitation_names_the_clinic_in_the_email_s_language(
        string language, string subject, string eyebrow, string title, string body, string button, string note, string reason)
    {
        var content = AccountEmails.Invitation(To, language, Link, "عيادة النور", "Al Noor Clinic", 72, SignIn);
        var message = Render(content);

        Assert.Equal((subject, eyebrow), (content.Subject, content.Eyebrow));
        Assert.Contains(title, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(body, message.TextBody, StringComparison.Ordinal);
        Assert.Equal((button, Link), (Button(content).Label, Button(content).Address));
        Assert.Contains(note, message.TextBody, StringComparison.Ordinal);
        // Where to sign in afterwards, kept left to right in an Arabic line (spec 015).
        Assert.Contains(language == "ar" ? "بعد تعيين كلمة المرور يكون الدخول من app.scs.test." : "Once the password is set, sign in at app.scs.test.", message.TextBody, StringComparison.Ordinal);
        Assert.Contains($"dir=\"ltr\"", message.HtmlBody, StringComparison.Ordinal);
        Assert.EndsWith(reason + "\n", message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(language == "ar" ? "Al Noor Clinic" : "عيادة النور", message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotMatch("[٠-٩]", message.TextBody + message.HtmlBody);
    }

    [Fact]
    public void A_clinic_with_one_name_is_named_by_it_in_both_languages()
    {
        Assert.Equal("Invitation to عيادة النور", AccountEmails.Invitation(To, "en", Link, "عيادة النور", "", 72, SignIn).Subject);
        Assert.Equal("دعوة إلى Al Noor Clinic", AccountEmails.Invitation(To, "ar", Link, " ", "Al Noor Clinic", 72, SignIn).Subject);
    }

    [Theory]
    [InlineData("ar", "أُنشئ حساب لك في عيادة النور", "حساب جديد", "أُنشئ لك حساب في عيادة النور على سمارت كلينك أو إس.", "كلمة المرور يسلّمها لك من أنشأ الحساب، ولا تُرسل بالبريد. عند أول دخول يُطلب منك اختيار كلمة مرور جديدة.", "تسجيل الدخول")]
    [InlineData("en", "An account was created for you at Al Noor Clinic", "NEW ACCOUNT", "An account was created for you at Al Noor Clinic on Smart Clinic OS.", "The password is handed to you by whoever created the account; it is never sent by email. At first sign-in you choose a new one.", "Sign in")]
    public void The_account_created_email_has_a_sign_in_button_and_no_password(
        string language, string subject, string eyebrow, string first, string second, string button)
    {
        var content = AccountEmails.AccountCreated(To, language, "عيادة النور", "Al Noor Clinic", SignIn);
        var message = Render(content);

        Assert.Equal((subject, eyebrow), (content.Subject, content.Eyebrow));
        Assert.Contains(first, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(second, message.TextBody, StringComparison.Ordinal);
        Assert.Equal((button, SignIn), (Button(content).Label, Button(content).Address));
        Assert.DoesNotMatch("[٠-٩]", message.TextBody + message.HtmlBody);
    }

    [Theory]
    [InlineData("ar", "أُعيد ضبط الدخول إلى حسابك", "أمان الحساب", "أعاد أحد أعضاء فريق العيادة ضبط الدخول إلى حسابك في سمارت كلينك أو إس، وكلمة المرور السابقة لم تعد تعمل.", "تسجيل الدخول", "إن لم يكن هذا متوقَّعًا، تواصل مع مسؤول العيادة أو فريق الدعم.")]
    [InlineData("en", "Access to your account was reset", "ACCOUNT SECURITY", "Someone on the clinic’s team reset access to your Smart Clinic OS account; the earlier password no longer works.", "Sign in", "If this was not expected, contact the clinic’s manager or support.")]
    public void The_access_reset_email_has_a_sign_in_button_and_no_password(
        string language, string subject, string eyebrow, string first, string button, string note)
    {
        var content = AccountEmails.AccessReset(To, language, SignIn);
        var message = Render(content);

        Assert.Equal((subject, eyebrow, subject), (content.Subject, content.Eyebrow, content.Title));
        Assert.Contains(first, message.TextBody, StringComparison.Ordinal);
        Assert.Equal((button, SignIn), (Button(content).Label, Button(content).Address));
        Assert.Contains(note, message.TextBody, StringComparison.Ordinal);
        InOneLanguage(message, language, SignIn);
    }

    [Fact]
    public void A_language_that_is_neither_is_arabic()
    {
        Assert.Equal("ar", AccountEmails.SignInCode(To, "fr", "427913", 10).Language);
        Assert.Equal("ar", AccountEmails.AccessReset(To, "", SignIn).Language);
    }
}
