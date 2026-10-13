using System.Net;
using System.Text.RegularExpressions;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Mail;

namespace Scs.UnitTests.Mail;

/// <summary>
/// Spec 094: what the template promises for whatever an email says — the shell, the direction,
/// each part, the text-only form, the footer's cases, and what it refuses.
/// </summary>
public partial class EmailRendererTests
{
    internal static readonly PlatformOptions Full = new()
    {
        NameAr = "سمارت كلينك أو إس",
        NameEn = "Smart Clinic OS",
        SupportEmail = "support@smartclinicos.com",
        SupportPhone = "+20 100 000 0000",
        AddressAr = "12 شارع التحرير، القاهرة",
        AddressEn = "12 Tahrir St., Cairo",
    };

    private const string To = "sara@noor.example";

    private static EmailMessage Render(EmailContent content, PlatformOptions? platform = null) =>
        EmailRenderer.Render(content, platform ?? Full);

    private static EmailContent Simple(string language, params EmailPart[] parts) => new(
        To, language, language == "en" ? "A subject" : "موضوع", language == "en" ? "SHORT LINE" : "سطر قصير",
        language == "en" ? "A title" : "عنوان", parts);

    private static int Count(string html, string part) => Regex.Count(html, $"data-part=\"{Regex.Escape(part)}\"");

    [Theory]
    [InlineData("ar", "rtl", "سمارت كلينك أو إس", "وصلتك هذه الرسالة بسبب نشاط على حسابك في سمارت كلينك أو إس.")]
    [InlineData("en", "ltr", "Smart Clinic OS", "You’re receiving this email because of activity on your Smart Clinic OS account.")]
    public void An_email_is_the_shell_in_its_language_and_direction(string language, string direction, string name, string reason)
    {
        var message = Render(Simple(language, new EmailParagraph("p")));

        Assert.Contains($"<html lang=\"{language}\" dir=\"{direction}\">", message.HtmlBody, StringComparison.Ordinal);
        Assert.Equal(1, Count(message.HtmlBody, "mark"));
        Assert.Contains(">S</td>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(message.HtmlBody, "<h1 "));
        Assert.Equal(2, Count(message.HtmlBody, "rule"));
        Assert.Contains($"data-part=\"platform\"", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(name), message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(reason), message.HtmlBody, StringComparison.Ordinal);

        // The sender's name is the platform's, in the email's language; the recipient is untouched.
        Assert.Equal((name, To), (message.SenderName, message.To));
        // Every layout table is scaffolding, and says so.
        Assert.Equal(Regex.Count(message.HtmlBody, "<table "), Regex.Count(message.HtmlBody, "<table role=\"presentation\""));
        // Nothing of an email when it is printed.
        Assert.Equal("EmailMessage", message.ToString());
    }

    [Fact]
    public void A_language_the_platform_does_not_have_is_Arabic()
    {
        Assert.Equal("ar", Simple("fr").Language);
        Assert.Contains("dir=\"rtl\"", Render(Simple("fr")).HtmlBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "short", "title", "reason")]
    [InlineData("subject", " ", "title", "reason")]
    [InlineData("subject", "short", "", "reason")]
    [InlineData("subject", "short", "title", " ")]
    public void An_email_without_its_subject_short_line_title_or_reason_is_refused_when_built(
        string subject, string eyebrow, string title, string reason)
    {
        Assert.Throws<EmailCompositionException>(() => new EmailContent(To, "en", subject, eyebrow, title, [], reason));
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("en")]
    public void An_optional_part_with_nothing_in_it_is_not_drawn_and_leaves_no_gap(string language)
    {
        // FR-016, contract §3: a note or a paragraph given nothing is left out of both forms,
        // and the email is exactly the one that never had it.
        var button = new EmailButton("Open", "https://app.scs.test/x");
        var with = Render(Simple(
            language, new EmailParagraph(""), new EmailParagraph("Words"), new EmailNote("  "), button, new EmailNote(null), new EmailParagraph(null)));
        var without = Render(Simple(language, new EmailParagraph("Words"), button));

        Assert.Equal(without.HtmlBody, with.HtmlBody);
        Assert.Equal(without.TextBody, with.TextBody);
        Assert.Equal((1, 0), (Count(with.HtmlBody, "paragraph"), Count(with.HtmlBody, "note")));

        // The line beside the subject is the first paragraph that says something.
        Assert.Contains(">Words</div>", with.HtmlBody, StringComparison.Ordinal);

        // What an email cannot be without is still refused.
        Assert.Throws<EmailCompositionException>(() => new EmailButton("Open", ""));
        Assert.Throws<EmailCompositionException>(() => new EmailButton(" ", "https://app.scs.test/x"));
        Assert.Throws<EmailCompositionException>(() => new EmailFact("Name", " "));
        Assert.Throws<EmailCompositionException>(() => new EmailFacts([]));
        Assert.Throws<EmailCompositionException>(() => new EmailCode(""));
        Assert.Throws<EmailCompositionException>(() => new EmailAttention(""));
    }

    [Fact]
    public void A_subject_and_a_title_are_cut_at_a_word_and_a_subject_never_names_the_platform()
    {
        var longOne = string.Join(' ', Enumerable.Repeat("word", 60));
        var content = new EmailContent(To, "en", longOne, "s", longOne);

        Assert.True(content.Subject.Length <= EmailContent.SubjectMaxLength);
        Assert.EndsWith("word…", content.Subject, StringComparison.Ordinal);
        Assert.True(content.Title.Length <= EmailContent.TitleMaxLength);
        Assert.EndsWith("word…", content.Title, StringComparison.Ordinal);
        Assert.Throws<EmailCompositionException>(() => new EmailContent(To, "en", "s", new string('e', 61), "t"));
        Assert.Throws<EmailCompositionException>(
            () => new EmailContent(To, "en", $"Welcome to {EmailContent.Platform}", "s", "t"));
    }

    [Theory]
    [InlineData("ar", "أو انسخ هذا الرابط في متصفحك:")]
    [InlineData("en", "Or copy this link into your browser:")]
    public void A_button_is_drawn_once_with_its_whole_address_written_out_under_it(string language, string words)
    {
        var address = "https://app.scs.test/password/reset#token=" + new string('a', 300);
        var message = Render(Simple(language, new EmailParagraph("p"), new EmailButton("Go", address)));

        Assert.Equal(1, Count(message.HtmlBody, "button"));
        Assert.Equal(1, Count(message.HtmlBody, "link"));
        Assert.Contains(WebUtility.HtmlEncode(words), message.HtmlBody, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(message.HtmlBody, Regex.Escape($"href=\"{address}\"")));
        Assert.Contains($">{address}</a>", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("…", message.HtmlBody, StringComparison.Ordinal);

        // In the text-only form: the label, then the address alone on its line.
        Assert.Contains($"Go:\n{address}\n", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void An_email_with_nothing_to_do_has_no_button_and_no_link_line()
    {
        var message = Render(Simple("en", new EmailParagraph("p")));

        Assert.Equal(0, Count(message.HtmlBody, "button"));
        Assert.Equal(0, Count(message.HtmlBody, "link"));
        Assert.DoesNotContain("copy this link", message.HtmlBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://evil.example/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("app.scs.test/reset")]
    public void A_button_leads_only_to_an_https_address(string address)
    {
        Assert.Throws<EmailCompositionException>(() => new EmailButton("Go", address));
    }

    [Fact]
    public void The_local_console_is_the_one_address_that_is_not_https_and_two_buttons_are_refused()
    {
        _ = new EmailButton("Go", "http://localhost:4203/password/first#token=abc");

        var one = new EmailButton("Go", "https://app.scs.test/a");
        Assert.Throws<EmailCompositionException>(() => Simple("en", one, one));
    }

    [Fact]
    public void Parts_are_drawn_in_the_order_given_in_both_forms()
    {
        var message = Render(Simple(
            "en", new EmailParagraph("first words"), new EmailNote("second words"), new EmailParagraph("third words")));

        foreach (var form in new[] { message.HtmlBody, message.TextBody })
        {
            var first = form.IndexOf("first words", StringComparison.Ordinal);
            var second = form.IndexOf("second words", StringComparison.Ordinal);
            var third = form.IndexOf("third words", StringComparison.Ordinal);
            Assert.True(first >= 0 && first < second && second < third);
        }
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("en")]
    public void A_code_is_six_digits_together_left_to_right_and_read_one_by_one(string language)
    {
        var message = Render(Simple(language, new EmailCode("742906")));

        // Spaced by the box, not by spaces: a copied code pastes as six digits.
        Assert.Contains("aria-label=\"7 4 2 9 0 6\">742906</span>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Matches("data-part=\"code\" dir=\"ltr\"", message.HtmlBody);
        Assert.Contains("\n742906\n", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("telephone=no", message.HtmlBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData("١٢٣٤٥٦")]
    public void What_is_not_six_Western_digits_is_not_a_code(string digits)
    {
        Assert.Throws<EmailCompositionException>(() => new EmailCode(digits));
    }

    [Fact]
    public void Facts_are_rows_of_a_name_and_a_value_with_an_optional_total_and_status()
    {
        var facts = new EmailFacts(
            [new EmailFact("Invoice", "INV-2026-0068"), new EmailFact("Due date", "Oct 25, 2026")],
            total: new EmailFact("Amount due", "EGP 568.86"),
            status: new EmailStatus("Suspended", EmailTone.Bad, "since Nov 3, 2026"));
        var message = Render(Simple("en", facts));

        Assert.Equal(1, Count(message.HtmlBody, "facts"));
        Assert.Equal(3, Count(message.HtmlBody, "fact"));
        Assert.Single(Regex.Matches(message.HtmlBody, "data-total=\"true\""));
        Assert.Equal(1, Count(message.HtmlBody, "mark-bad"));
        // The name at the start of the line, the value at the far edge.
        Assert.Matches("text-align:left;[^>]*>Invoice</td><td[^>]*text-align:right;[^>]*>INV-2026-0068", message.HtmlBody);

        Assert.Contains("Invoice: INV-2026-0068\nDue date: Oct 25, 2026\nAmount due: EGP 568.86\nSuspended — since Nov 3, 2026\n", message.TextBody, StringComparison.Ordinal);

        Assert.Throws<EmailCompositionException>(() => new EmailFacts([]));
        Assert.Throws<EmailCompositionException>(() => new EmailFact("name", " "));
    }

    [Fact]
    public void In_Arabic_a_fact_s_name_is_at_the_right_and_its_value_at_the_left()
    {
        var message = Render(Simple("ar", new EmailFacts([new EmailFact("الوقت", "2026-10-12 14:05")])));
        Assert.Matches("text-align:right;[^>]*>الوقت</td><td[^>]*text-align:left;", message.HtmlBody);
    }

    [Theory]
    [InlineData(EmailTone.Neutral)]
    [InlineData(EmailTone.Good)]
    [InlineData(EmailTone.Warning)]
    [InlineData(EmailTone.Bad)]
    public void The_attention_box_has_its_mark_its_line_and_a_quieter_line_in_each_tone(EmailTone tone)
    {
        var name = tone.ToString().ToLowerInvariant();
        var message = Render(Simple(
            "en", new EmailAttention("T-1042 — Unable to access", "Al Noor Clinic · Opened yesterday", new EmailStatus("Overdue", tone), tone)));

        Assert.Equal(1, Count(message.HtmlBody, "attention"));
        Assert.Equal(1, Count(message.HtmlBody, $"mark-{name}"));
        Assert.Contains($"background:{EmailPalette.Attention(tone)}", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Overdue\nT-1042 — Unable to access\nAl Noor Clinic · Opened yesterday\n", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_only_form_is_the_same_words_in_the_same_order_with_no_markup()
    {
        var message = Render(Simple(
            "en", new EmailParagraph("Body words."), new EmailCode("742906"), new EmailNote("A note.")));

        Assert.Equal(
            "SHORT LINE\nA title\n\nBody words.\n\n742906\n\nA note.\n\n--\nSmart Clinic OS\n12 Tahrir St., Cairo\n" +
            "support@smartclinicos.com · +20 100 000 0000\n" +
            "You’re receiving this email because of activity on your Smart Clinic OS account.\n",
            message.TextBody);
    }

    [Fact]
    public void The_line_shown_beside_the_subject_is_the_first_paragraph_hidden_from_page_and_reader()
    {
        var withBody = Render(Simple("en", new EmailNote("a note"), new EmailParagraph("First words of substance."))).HtmlBody;
        Assert.Matches("<div aria-hidden=\"true\" data-part=\"preview\" style=\"display:none;[^\"]*\">First words of substance.</div>", withBody);

        // With no paragraph, the title.
        Assert.Matches("data-part=\"preview\"[^>]*>A title</div>", Render(Simple("en")).HtmlBody);
    }

    [Fact]
    public void The_platform_s_name_is_written_by_the_template_in_the_email_s_language()
    {
        var words = $"Welcome to {EmailContent.Platform}.";

        Assert.Contains("Welcome to Smart Clinic OS.", Render(Simple("en", new EmailParagraph(words))).HtmlBody, StringComparison.Ordinal);
        var arabic = Render(Simple("ar", new EmailParagraph($"مرحبًا بك في {EmailContent.Platform}.")));
        Assert.Contains(WebUtility.HtmlEncode("مرحبًا بك في سمارت كلينك أو إس."), arabic.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("مرحبًا بك في سمارت كلينك أو إس.", arabic.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(EmailContent.Platform, arabic.HtmlBody + arabic.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_marked_left_to_right_keeps_its_order_in_an_Arabic_line()
    {
        var message = Render(Simple("ar", new EmailParagraph($"أرسلنا الفاتورة {EmailContent.LeftToRight("INV-2026-0068")} إلى بريدك.")));

        Assert.Contains("<span dir=\"ltr\" style=\"unicode-bidi:isolate;white-space:nowrap;\">INV-2026-0068</span>", message.HtmlBody, StringComparison.Ordinal);
        // The marks themselves are in neither form.
        Assert.DoesNotContain(EmailMarks.Open, message.HtmlBody + message.TextBody);
        Assert.DoesNotContain(EmailMarks.Close, message.HtmlBody + message.TextBody);
        Assert.Contains("أرسلنا الفاتورة INV-2026-0068 إلى بريدك.", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void What_a_person_wrote_cannot_become_markup_or_a_link()
    {
        const string Written = "<b>bold</b> <a href=\"https://evil.example\">x</a> \"q\" & <script>";
        var message = Render(new EmailContent(
            To, "en", "s", "short", Written,
            [new EmailParagraph(Written), new EmailNote(Written), new EmailFacts([new EmailFact("name", Written)])]));

        Assert.DoesNotContain("<b>", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"https://evil.example\"", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;bold&lt;/b&gt;", message.HtmlBody, StringComparison.Ordinal);
        // The only link is the support address.
        Assert.Single(Regex.Matches(message.HtmlBody, "<a "));
        // As written, in the text-only form.
        Assert.Contains(Written, message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void The_footer_shows_what_is_set_and_leaves_out_what_is_not()
    {
        var all = Render(Simple("en")).HtmlBody;
        Assert.Equal((1, 1, 1), (Count(all, "address"), Count(all, "phone"), Count(all, "support")));
        Assert.Contains("href=\"mailto:support@smartclinicos.com\"", all, StringComparison.Ordinal);
        Assert.Contains(" · ", all, StringComparison.Ordinal);

        var bare = new PlatformOptions { NameAr = "منصّة", NameEn = "Platform", SupportEmail = "help@p.test" };
        var message = Render(Simple("en"), bare);
        Assert.Equal((0, 0, 1), (Count(message.HtmlBody, "address"), Count(message.HtmlBody, "phone"), Count(message.HtmlBody, "support")));
        Assert.DoesNotContain(" · ", message.HtmlBody, StringComparison.Ordinal);
        Assert.EndsWith(
            "--\nPlatform\nhelp@p.test\nYou’re receiving this email because of activity on your Platform account.\n",
            message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void An_address_set_in_one_language_is_shown_as_set_in_the_other()
    {
        var onlyEnglish = new PlatformOptions
        {
            NameAr = "منصّة", NameEn = "Platform", SupportEmail = "help@p.test", AddressEn = "12 Tahrir St., Cairo",
        };

        Assert.Matches("data-part=\"address\" dir=\"auto\"[^>]*>12 Tahrir St., Cairo</p>", Render(Simple("ar"), onlyEnglish).HtmlBody);
    }

    [Fact]
    public void Changing_the_platform_s_name_or_support_email_changes_them_and_nothing_else()
    {
        var content = Simple("en", new EmailParagraph($"Welcome to {EmailContent.Platform}."), new EmailButton("Go", "https://app.scs.test/x"));
        var before = Render(content);
        var after = Render(content, new PlatformOptions
        {
            NameAr = Full.NameAr, NameEn = "Clinic Cloud", SupportEmail = "care@cloud.test",
            SupportPhone = Full.SupportPhone, AddressAr = Full.AddressAr, AddressEn = Full.AddressEn,
        });

        string Back(string form) => form
            .Replace("Clinic Cloud", "Smart Clinic OS", StringComparison.Ordinal)
            .Replace("care@cloud.test", "support@smartclinicos.com", StringComparison.Ordinal);

        Assert.Equal(before.HtmlBody, Back(after.HtmlBody));
        Assert.Equal(before.TextBody, Back(after.TextBody));
        Assert.Equal("Clinic Cloud", after.SenderName);
    }

    [Fact]
    public void The_reason_line_is_the_email_s_own_when_it_gives_one()
    {
        var content = new EmailContent(To, "en", "s", "short", "title", [], "You’re receiving this because you asked.");
        var message = Render(content);

        Assert.Contains("You’re receiving this because you asked.", WebUtility.HtmlDecode(message.HtmlBody), StringComparison.Ordinal);
        Assert.DoesNotContain("because of activity", message.TextBody, StringComparison.Ordinal);
    }
}
