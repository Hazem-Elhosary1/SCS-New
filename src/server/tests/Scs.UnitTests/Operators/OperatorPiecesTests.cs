using System.Text.RegularExpressions;
using Scs.BuildingBlocks.Mail;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure;
using Scs.UnitTests.Mail;

namespace Scs.UnitTests.Operators;

/// <summary>
/// Spec 010: the emails an operator receives, the lines of the sign-in record, and the rules of
/// the account itself.
/// </summary>
public partial class OperatorPiecesTests
{
    [GeneratedRegex(@"\p{IsArabic}")]
    private static partial Regex ArabicLetter();

    private const string To = "sara@heta.example";

    private static EmailMessage Render(EmailContent content) => EmailRenderer.Render(content, EmailRendererTests.Full);

    [Theory]
    [InlineData("ar", "رمز التحقق", "رمز التحقق", "تأكيد هويتك", "استخدم هذا الرمز لإكمال تسجيل الدخول إلى سمارت كلينك أو إس.", "ينتهي هذا الرمز خلال 10 دقائق.", "لن يطلب منك أحد من سمارت كلينك أو إس هذا الرمز.")]
    [InlineData("en", "Your verification code", "VERIFICATION CODE", "Confirm it’s you", "Use this code to finish signing in to Smart Clinic OS.", "This code expires in 10 minutes.", "Nobody from Smart Clinic OS will ask you for this code.")]
    public void The_code_s_email_is_in_one_language_and_holds_its_code_once_and_no_link(
        string language, string subject, string eyebrow, string title, string body, string life, string nobody)
    {
        var content = OperatorEmails.Code(To, language, "427913", 10, ChallengePurposes.SignIn);
        var message = Render(content);

        Assert.Equal((To, subject, eyebrow, title), (message.To, message.Subject, content.Eyebrow, content.Title));
        Assert.Equal(language == "ar" ? "سمارت كلينك أو إس" : "Smart Clinic OS", message.SenderName);
        Assert.Contains(body, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(life, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(nobody, message.TextBody, StringComparison.Ordinal);
        Assert.Equal(3, content.Parts.OfType<EmailNote>().Count());

        Assert.Single(Regex.Matches(message.TextBody, "427913"));
        Assert.Empty(content.Parts.OfType<EmailButton>());
        Assert.DoesNotContain("http", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-part=\"button\"", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(language == "ar" ? "dir=\"rtl\"" : "dir=\"ltr\"", message.HtmlBody, StringComparison.Ordinal);

        // One language only; the subject is plain: no platform name and no tag.
        Assert.Equal(language == "ar", ArabicLetter().IsMatch(message.TextBody.Replace("سمارت كلينك أو إس", "", StringComparison.Ordinal)));
        Assert.DoesNotContain("Smart Clinic OS", message.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("[", message.Subject, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ar", "رمز تأكيد تغيير كلمة المرور", "استخدم هذا الرمز لتأكيد تغيير كلمة مرورك في لوحة التشغيل.", "لم تطلب تغيير كلمة المرور؟ لا تُدخل الرمز، وغيّر كلمة مرورك الآن.")]
    [InlineData("en", "Code to confirm your password change", "Use this code to confirm the change of your Ops Console password.", "Didn’t ask to change your password? Don’t enter the code, and change your password now.")]
    public void The_code_for_a_change_of_password_says_what_it_is_for(string language, string subject, string body, string note)
    {
        var message = Render(OperatorEmails.Code(To, language, "427913", 10, ChallengePurposes.PasswordChange));

        Assert.Equal(subject, message.Subject);
        Assert.Contains(body, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(note, message.TextBody, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(message.TextBody, "427913"));
    }

    [Fact]
    public void The_link_emails_hold_one_button_with_their_link_and_how_long_it_lives()
    {
        const string Link = "https://ops.scs.test/password/reset#token=abc";

        var reset = OperatorEmails.ResetLink(To, "en", Link, 30);
        var resetMessage = Render(reset);
        Assert.Equal(("Reset your password", "PASSWORD RESET", "Choose a new password"), (reset.Subject, reset.Eyebrow, reset.Title));
        Assert.Equal(("Reset password", Link), (reset.Parts.OfType<EmailButton>().Single().Label, reset.Parts.OfType<EmailButton>().Single().Address));
        Assert.Single(Regex.Matches(resetMessage.TextBody, "https://"));
        Assert.Contains("expires in 30 minutes", resetMessage.TextBody, StringComparison.Ordinal);
        Assert.Contains("your password stays the same", resetMessage.TextBody, StringComparison.Ordinal);

        var first = OperatorEmails.FirstPassword(To, "ar", Link, 72);
        var firstMessage = Render(first);
        Assert.Equal(("دعوة إلى لوحة التشغيل", "دعوة", "انضم إلى لوحة التشغيل"), (first.Subject, first.Eyebrow, first.Title));
        Assert.Equal(("تعيين كلمة المرور", Link), (first.Parts.OfType<EmailButton>().Single().Label, first.Parts.OfType<EmailButton>().Single().Address));
        Assert.Single(Regex.Matches(firstMessage.TextBody, "https://"));
        Assert.Contains("ينتهي الرابط خلال 72 ساعة", firstMessage.TextBody, StringComparison.Ordinal);
        Assert.Contains("وصلتك هذه الرسالة لأن أحد المسؤولين أضافك إلى لوحة التشغيل.", firstMessage.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Smart Clinic OS", first.Subject + firstMessage.TextBody, StringComparison.Ordinal);

        var firstEnglish = Render(OperatorEmails.FirstPassword(To, "en", Link, 72));
        Assert.Contains("You were added to the Smart Clinic OS Ops Console.", firstEnglish.TextBody, StringComparison.Ordinal);
        Assert.Contains("expires in 72 hours", firstEnglish.TextBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en", "New sign-in to your account", "Browser: Chrome on Windows", "Address: 41.233.14.89", "When: 12 October 2026, 12:00 PM", "reset your password now")]
    [InlineData("ar", "تسجيل دخول من متصفح جديد", "المتصفح: متصفح Chrome على Windows", "العنوان: 41.233.14.89", "الوقت: 12 أكتوبر 2026، 12:00 م", "أعد تعيين كلمة مرورك الآن")]
    public void The_new_browser_notice_says_what_where_from_and_when_and_what_to_do(
        string language, string subject, string browser, string address, string when, string toDo)
    {
        var content = OperatorEmails.NewBrowser(
            To, language, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/140.0 Safari/537.36", "41.233.14.89",
            new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
        var message = Render(content);

        Assert.Equal(subject, message.Subject);
        Assert.Contains(browser, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(address, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(when, message.TextBody, StringComparison.Ordinal); // Cairo time, Western digits
        Assert.Contains(toDo, message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(content.Parts.OfType<EmailButton>());
        Assert.DoesNotContain("http", message.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_new_browser_notice_leaves_out_an_address_it_does_not_know()
    {
        var content = OperatorEmails.NewBrowser(To, "en", null, null, DateTimeOffset.UnixEpoch);
        Assert.Equal(["Browser", "When"], content.Parts.OfType<EmailFacts>().Single().Rows.Select(row => row.Name));
    }

    [Fact]
    public void A_record_is_of_a_known_kind_and_its_note_is_cut_to_size()
    {
        var now = DateTimeOffset.UnixEpoch;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SignInRecord.Of("something_else", null, now, null, null, "trace"));

        var record = SignInRecord.Of(
            SignInRecordKinds.SessionEnded, Guid.NewGuid(), now, new string('1', 80), new string('a', 400), "trace",
            facts: new string('x', 900));

        Assert.Equal(SignInChallenge.AddressMaxLength, record.Address!.Length);
        Assert.Equal(OperatorSession.UserAgentMaxLength, record.UserAgent!.Length);
        Assert.Equal(SignInRecord.FactsMaxLength, record.Facts!.Length);
        Assert.Equal(15, SignInRecordKinds.All.Count);
        Assert.Equal(SignInRecordKinds.All.Count, SignInRecordKinds.All.Distinct().Count());
    }

    [Fact]
    public void An_operator_is_invited_then_chooses_a_password_and_a_new_password_forgets_the_lock()
    {
        var now = DateTimeOffset.UnixEpoch;
        var account = Operator.Invite("  Sara Ahmed ", To, now);

        Assert.Equal(("Sara Ahmed", OperatorStates.Invited, "ar"), (account.FullName, account.State, account.Language));
        Assert.Null(account.PasswordHash);
        Assert.Throws<ArgumentException>(() => Operator.Invite("", To, now));
        Assert.Throws<ArgumentException>(() => Operator.Invite(new string('n', 201), To, now));

        account.OweInvitation(now);
        Assert.NotNull(account.InvitationOwedSinceUtc);

        account.Activate("v1.hash", now);
        Assert.Equal(OperatorStates.Active, account.State);
        Assert.Null(account.InvitationOwedSinceUtc);
        Assert.Throws<InvalidOperationException>(() => account.Activate("v1.other", now));
        Assert.Throws<InvalidOperationException>(() => account.OweInvitation(now));
        Assert.False(account.IsLocked(now));
    }

    [Theory]
    [InlineData("sara@heta.example", true)]
    [InlineData("  Sara@Heta.Example ", true)]
    [InlineData("sara@heta", false)]
    [InlineData("sara heta@example.com", false)]
    [InlineData("@heta.example", false)]
    [InlineData("sara@@heta.example", false)]
    [InlineData("", false)]
    public void An_email_is_compared_trimmed_and_in_lower_case(string email, bool valid)
    {
        Assert.Equal(valid, OperatorEmail.LooksValid(email));
        Assert.Equal(email.Trim().ToLowerInvariant(), OperatorEmail.Normalize(email));
    }

    [Fact]
    public void What_the_operator_did_themselves_is_not_told_back_to_them()
    {
        Assert.True(SessionEndings.ByTheOperator(SessionEndings.SignedOut));
        Assert.True(SessionEndings.ByTheOperator(SessionEndings.SignedOutEverywhere));
        Assert.False(SessionEndings.ByTheOperator(SessionEndings.Replaced));
        Assert.Equal(10, SessionEndings.All.Count);
    }
}
