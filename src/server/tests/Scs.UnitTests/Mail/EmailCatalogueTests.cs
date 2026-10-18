using System.Net;
using System.Text.RegularExpressions;
using Scs.BuildingBlocks.Mail;

namespace Scs.UnitTests.Mail;

/// <summary>
/// Spec 094: what every email of the catalogue keeps, whichever module wrote it — it loads
/// nothing (FR-019), its text-only form says the same in the same order (FR-022), it reads for
/// a screen reader (FR-018), and it carries only the platform's links, a plain subject and
/// Western digits (FR-009, FR-011, FR-034, FR-035).
/// </summary>
public partial class EmailCatalogueTests
{
    /// <summary>Where a sample's button may lead: the consoles' sample addresses.</summary>
    private static readonly string[] Consoles = ["https://app.scs.test/", "https://ops.scs.test/"];

    private static readonly string[] Loads = ["<img", "url(", "@import", "<link", "src=", "<script", "<iframe", "<form", "background="];
    private static readonly string[] Unsubscribing = ["unsubscribe", "stop receiving", "إلغاء الاشتراك", "إيقاف الرسائل"];
    private static readonly string[] CopyWords = ["Or copy this link into your browser:", "أو انسخ هذا الرابط في متصفحك:"];

    public static TheoryData<string> Slugs() => new(EmailCatalogueExport.AllSamples().Select(sample => sample.Slug));

    private static (EmailSample Sample, EmailMessage Message) Open(string slug)
    {
        var sample = EmailCatalogueExport.AllSamples().Single(s => s.Slug == slug);
        return (sample, EmailRenderer.Render(sample.Content, EmailCatalogueExport.Identity));
    }

    [GeneratedRegex("href=\"([^\"]*)\"")]
    private static partial Regex Href();

    [GeneratedRegex("<style>.*?</style>", RegexOptions.Singleline)]
    private static partial Regex StyleBlock();

    [GeneratedRegex("""<div aria-hidden="true" data-part="preview"[^>]*>(.*?)</div>""", RegexOptions.Singleline)]
    private static partial Regex Preview();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();

    [Fact]
    public void The_catalogue_holds_the_twelve_emails_and_the_template_s_samples_in_both_languages()
    {
        var samples = EmailCatalogueExport.AllSamples();

        Assert.Equal(
            [
                "clinic:AccessReset", "clinic:AccountCreated", "clinic:EmailChanged", "clinic:Invitation", "clinic:Notification", "clinic:PasswordChanged",
                "clinic:PasswordReset", "clinic:SignInCode", "operators:Code", "operators:Code:change", "operators:FirstPassword", "operators:FirstPassword:invited",
                "operators:NewBrowser", "operators:ResetLink", "sample:Invoice", "sample:LongContent", "sample:Shell",
                "sample:SubscriptionNotice", "sample:TicketNotification",
            ],
            samples.Select(s => string.Join(':', new[] { s.Audience, s.Kind, s.Variant }.Where(part => part.Length > 0)))
                .Distinct().Order(StringComparer.Ordinal));

        Assert.All(
            samples.GroupBy(s => (s.Audience, s.Kind, s.Variant)),
            kind => Assert.Equal(["ar", "en"], kind.Select(s => s.Language).Order(StringComparer.Ordinal)));
        Assert.Equal(samples.Count, samples.Select(s => s.Slug).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(Slugs))]
    public void An_email_loads_nothing_and_links_only_to_the_platform(string slug)
    {
        var (sample, message) = Open(slug);
        var html = message.HtmlBody;

        Assert.All(Loads, load => Assert.DoesNotContain(load, html, StringComparison.OrdinalIgnoreCase));

        var buttons = sample.Content.Parts.OfType<EmailButton>().ToList();
        Assert.True(buttons.Count <= 1);
        if (sample.Content.Parts.OfType<EmailCode>().Any())
        {
            Assert.Empty(buttons);
        }

        // The button, its written-out copy, and the support email: nothing else is a link.
        var links = Href().Matches(html).Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToList();
        var expected = buttons.SelectMany(button => new[] { button.Address, button.Address })
            .Append($"mailto:{EmailCatalogueExport.Identity.SupportEmail}");
        Assert.Equal(expected.Order(StringComparer.Ordinal), links.Order(StringComparer.Ordinal));
        Assert.All(buttons, button => Assert.Contains(Consoles, console => button.Address.StartsWith(console, StringComparison.Ordinal)));

        // No address anywhere else either: not in a style, not as text that a program might fetch.
        var withoutLinks = buttons.Aggregate(html, (all, button) => all.Replace(WebUtility.HtmlEncode(button.Address), "", StringComparison.Ordinal));
        Assert.DoesNotContain("http", withoutLinks, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(Slugs))]
    public void An_email_s_text_only_form_says_the_same_words_in_the_same_order(string slug)
    {
        var (_, message) = Open(slug);
        var text = message.TextBody;

        Assert.DoesNotContain("<", text, StringComparison.Ordinal);
        Assert.DoesNotContain(">", text, StringComparison.Ordinal);
        Assert.DoesNotMatch("&[a-z]+;|&#", text);
        Assert.DoesNotContain(EmailContent.Platform, text + message.HtmlBody + message.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain(EmailMarks.Open, text);
        Assert.DoesNotContain(EmailMarks.Close, text);

        // The page's words, without what only the page has: the mark, the hidden preview line,
        // the page's title and the words that introduce the written-out link.
        var body = message.HtmlBody[message.HtmlBody.IndexOf("<body", StringComparison.Ordinal)..];
        body = Preview().Replace(StyleBlock().Replace(body, ""), "");
        body = Regex.Replace(body, """<td[^>]*data-part="mark"[^>]*>S</td>""", "");
        var page = WebUtility.HtmlDecode(Tag().Replace(body, " "));
        page = CopyWords.Aggregate(page, (all, words) => all.Replace(words, " ", StringComparison.Ordinal));

        var pageWords = Word().Matches(page).Select(match => match.Value).ToList();
        var textWords = Word().Matches(text).Select(match => match.Value).ToList();

        // Every word of the page is in the text, in order; a button's address is there once, not twice.
        var at = 0;
        var lost = new List<string>();
        foreach (var word in pageWords)
        {
            var found = textWords.IndexOf(word, at);
            if (found < 0)
            {
                lost.Add(word);
            }
            else
            {
                at = found + 1;
            }
        }

        var address = string.Concat(Open(slug).Sample.Content.Parts.OfType<EmailButton>().Select(button => button.Address));
        var repeated = Word().Matches(address).Select(match => match.Value).ToList();
        Assert.True(
            lost.Count <= repeated.Count && lost.All(repeated.Contains),
            "Words of the page that the text-only form lacks: " + string.Join(' ', lost.Except(repeated)));
    }

    [Theory]
    [MemberData(nameof(Slugs))]
    public void An_email_reads_for_a_screen_reader(string slug)
    {
        var (sample, message) = Open(slug);
        var html = message.HtmlBody;
        var direction = sample.Language == "ar" ? "rtl" : "ltr";

        Assert.Contains($"<html lang=\"{sample.Language}\" dir=\"{direction}\">", html, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(html, "<h1[ >]"));
        Assert.Equal(Regex.Count(html, "<table"), Regex.Count(html, "<table role=\"presentation\""));

        var first = sample.Content.Parts.OfType<EmailParagraph>().FirstOrDefault()?.Text ?? sample.Content.Title;
        var preview = WebUtility.HtmlDecode(Preview().Match(html).Groups[1].Value);
        Assert.Equal(Plain(first, sample.Language), preview);
    }

    [Theory]
    [MemberData(nameof(Slugs))]
    public void An_email_s_subject_is_plain_and_its_digits_are_western(string slug)
    {
        var (sample, message) = Open(slug);

        Assert.InRange(message.Subject.Length, 1, EmailContent.SubjectMaxLength);
        Assert.DoesNotContain("[", message.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain(EmailCatalogueExport.Identity.NameAr, message.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain(EmailCatalogueExport.Identity.NameEn, message.Subject, StringComparison.Ordinal);
        Assert.Equal(EmailCatalogueExport.Identity.Name(sample.Language), message.SenderName);

        var everything = message.Subject + message.TextBody + message.HtmlBody;
        Assert.DoesNotMatch("[٠-٩۰-۹]", everything);
        Assert.All(Unsubscribing, words => Assert.DoesNotContain(words, everything, StringComparison.OrdinalIgnoreCase));

        // An email names nobody's address but the support's: the recipient's is not in it.
        Assert.DoesNotContain(sample.Content.To, everything, StringComparison.OrdinalIgnoreCase);
    }

    private static string Plain(string words, string language) => words
        .Replace(EmailContent.Platform, EmailCatalogueExport.Identity.Name(language), StringComparison.Ordinal)
        .Replace(EmailMarks.Open.ToString(), "", StringComparison.Ordinal)
        .Replace(EmailMarks.Close.ToString(), "", StringComparison.Ordinal);
}
