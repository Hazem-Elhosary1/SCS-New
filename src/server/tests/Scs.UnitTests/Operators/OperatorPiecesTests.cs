using Scs.Operators.Domain;
using Scs.Operators.Infrastructure;

namespace Scs.UnitTests.Operators;

/// <summary>
/// Spec 010: the emails an operator receives, the lines of the sign-in record, and the rules of
/// the account itself.
/// </summary>
public class OperatorPiecesTests
{
    private const string To = "sara@heta.example";

    [Theory]
    [InlineData("ar", "رمز الدخول")]
    [InlineData("en", "Sign-in code")]
    public void The_code_s_email_is_in_one_language_names_the_platform_and_holds_no_link(string language, string subject)
    {
        var message = OperatorEmails.Code(To, language, "427913", 10, ChallengePurposes.SignIn);

        Assert.Equal(To, message.To);
        Assert.Contains(subject, message.Subject, StringComparison.Ordinal);
        Assert.Contains("Smart Clinic OS", message.Subject, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(message.TextBody, "427913"));
        Assert.DoesNotContain("http", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(language == "ar" ? "dir=\"rtl\"" : "dir=\"ltr\"", message.HtmlBody, StringComparison.Ordinal);

        // One language only: the other's words are not there.
        Assert.DoesNotContain(language == "ar" ? "Sign-in code" : "رمز الدخول", message.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void The_code_for_a_change_of_password_says_what_it_is_for()
    {
        var message = OperatorEmails.Code(To, "en", "427913", 10, ChallengePurposes.PasswordChange);
        Assert.Contains("change your password", message.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_link_emails_hold_their_link_once_and_how_long_it_lives()
    {
        const string Link = "https://ops.scs.test/password/reset#token=abc";

        var reset = OperatorEmails.ResetLink(To, "en", Link, 30);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(reset.TextBody, "https://"));
        Assert.Contains("30 minutes", reset.TextBody, StringComparison.Ordinal);

        var first = OperatorEmails.FirstPassword(To, "ar", Link, 72);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(first.TextBody, "https://"));
        Assert.Contains("72", first.TextBody, StringComparison.Ordinal);
        Assert.Contains("Smart Clinic OS", first.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void The_new_browser_notice_says_what_where_from_and_when_and_what_to_do()
    {
        var message = OperatorEmails.NewBrowser(
            To, "en", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/140.0 Safari/537.36", "41.233.14.89",
            new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));

        Assert.Contains("41.233.14.89", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("2026-10-12 12:00", message.TextBody, StringComparison.Ordinal); // Cairo time
        Assert.Contains("reset your password", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", message.TextBody, StringComparison.OrdinalIgnoreCase);
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
