using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;

namespace Scs.BuildingBlocks.Mail;

/// <summary>
/// Turns what an email says into the two forms a mail program needs (spec 094): the laid-out
/// message of the design's "Email Template" page, and a text-only message made from the same
/// content in the same order. It alone writes markup, the footer, the direction and the
/// platform's name, so no email can get them wrong or be written twice.
/// <para>
/// The markup is what mail programs are known to keep: tables for layout, every style on its
/// element, widths as a fluid maximum. The one style block only adds the dark theme; the
/// message is right without it. Nothing is loaded from anywhere (FR-019).
/// </para>
/// </summary>
public sealed class EmailRenderer(IOptions<ScsOptions> options)
{
    public EmailMessage Render(EmailContent content) => Render(content, options.Value.Platform);

    public static EmailMessage Render(EmailContent content, PlatformOptions platform)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(platform);

        var page = new Page(content, platform);
        return new EmailMessage(content.To, content.Subject, page.Text(), page.Html()) { SenderName = page.Name };
    }

    private sealed class Page(EmailContent content, PlatformOptions platform)
    {
        private const string Font = EmailPalette.FontStack;

        private readonly bool english = content.Language == "en";

        public string Name { get; } = platform.Name(content.Language);

        private string Direction => english ? "ltr" : "rtl";

        private string Start => english ? "left" : "right";

        private string End => english ? "right" : "left";

        private string CopyWords => english ? "Or copy this link into your browser:" : "أو انسخ هذا الرابط في متصفحك:";

        private string Reason => content.Reason ?? (english
            ? $"You’re receiving this email because of activity on your {EmailContent.Platform} account."
            : $"وصلتك هذه الرسالة بسبب نشاط على حسابك في {EmailContent.Platform}.");

        // ------------------------------------------------------------------ the laid-out form

        public string Html()
        {
            var html = new StringBuilder(8_192);
            html.Append(CultureInfo.InvariantCulture, $"""
                <!doctype html>
                <html lang="{content.Language}" dir="{Direction}">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta name="color-scheme" content="light dark">
                <meta name="supported-color-schemes" content="light dark">
                <meta name="format-detection" content="telephone=no, date=no, address=no, email=no">
                <title>{Words(content.Subject)}</title>
                <style>
                {DarkTheme()}
                </style>
                </head>
                <body class="e-ground" style="margin:0;padding:0;background:{EmailPalette.Ground};">
                <div aria-hidden="true" data-part="preview" style="display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;">{Encode(Plain(Preview()))}</div>
                <table role="presentation" class="e-ground" dir="{Direction}" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{EmailPalette.Ground};">
                <tr><td align="center" style="padding:36px 16px;">
                <table role="presentation" class="e-card" data-part="card" dir="{Direction}" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:{EmailPalette.CardWidth}px;background:{EmailPalette.Card};border-radius:{EmailPalette.CardRadius}px;">
                <tr><td align="center" data-part="header" style="padding:24px 24px 27px;">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr><td align="center" width="36" height="36" data-part="mark" style="width:36px;height:36px;background:{EmailPalette.Purple};border-radius:10px;color:{EmailPalette.OnPurple};font:700 18px/36px {Font};">S</td></tr></table>
                </td></tr>
                {RuleRow()}
                <tr><td align="center" data-part="body" style="padding:32px 8% 40px;text-align:center;">
                <p class="e-purple" data-part="eyebrow" style="margin:0;font:700 {(english ? "11px/16px" : "12px/18px")} {Font};color:{EmailPalette.Purple};{(english ? "letter-spacing:0.08em;" : "")}">{Words(content.Eyebrow)}</p>
                <h1 class="e-title" data-part="title" style="margin:14px 0 0;font:700 24px/34px {Font};color:{EmailPalette.Title};">{Words(content.Title)}</h1>

                """);

            foreach (var part in content.Parts.Where(part => !part.IsEmpty))
            {
                Draw(part, html);
            }

            html.Append(CultureInfo.InvariantCulture, $"""
                </td></tr>
                {RuleRow()}
                <tr><td align="center" data-part="footer" style="padding:28px 8% 30px;text-align:center;">
                <p class="e-body" data-part="platform" style="margin:0;font:600 12px/16px {Font};color:{EmailPalette.Body};">{Encode(Name)}</p>

                """);

            var address = platform.Address(content.Language);
            if (address.Length > 0)
            {
                html.Append(CultureInfo.InvariantCulture, $"""
                    <p class="e-note" data-part="address" dir="auto" style="margin:6px 0 0;font:400 11px/16px {Font};color:{EmailPalette.Note};">{Encode(address)}</p>

                    """);
            }

            var support = platform.Support();
            var phone = platform.Phone();
            html.Append(CultureInfo.InvariantCulture, $"""
                <p class="e-purple" data-part="support" dir="ltr" style="margin:8px 0 0;font:500 11px/16px {Font};color:{EmailPalette.Purple};"><a class="e-purple" href="mailto:{Encode(support)}" dir="ltr" style="color:{EmailPalette.Purple};text-decoration:none;">{Encode(support)}</a>
                """);
            if (phone.Length > 0)
            {
                html.Append(CultureInfo.InvariantCulture, $"""&nbsp; · &nbsp;<span dir="ltr" data-part="phone" style="unicode-bidi:isolate;">{Encode(phone)}</span>""");
            }

            html.Append(CultureInfo.InvariantCulture, $"""
                </p>
                <p class="e-note" data-part="reason" style="margin:8px 0 0;font:400 11px/16px {Font};color:{EmailPalette.Note};">{Words(Reason)}</p>
                </td></tr>
                </table>
                </td></tr>
                </table>
                </body>
                </html>

                """);

            return html.ToString();
        }

        private void Draw(EmailPart part, StringBuilder html)
        {
            switch (part)
            {
                case EmailParagraph paragraph:
                    html.Append(CultureInfo.InvariantCulture, $"""
                        <p class="e-body" data-part="paragraph" style="margin:16px 4% 0;font:400 14px/26px {Font};color:{EmailPalette.Body};">{Words(paragraph.Text)}</p>

                        """);
                    break;

                case EmailNote note:
                    html.Append(CultureInfo.InvariantCulture, $"""
                        <p class="e-note" data-part="note" style="margin:14px 0 0;font:400 12px/20px {Font};color:{EmailPalette.Note};">{Words(note.Text)}</p>

                        """);
                    break;

                case EmailCode code:
                    // One run of digits, spaced by the box and not by spaces, so that a copied
                    // code pastes as six digits; read out one digit at a time.
                    var spoken = string.Join(' ', code.Digits.ToCharArray());
                    html.Append(CultureInfo.InvariantCulture, $"""
                        <table role="presentation" align="center" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:24px auto 0;max-width:300px;"><tr><td class="e-code" data-part="code" dir="ltr" align="center" style="padding:8px 0 8px 12px;text-align:center;background:{EmailPalette.CodeGround};border:1px solid {EmailPalette.CodeBorder};border-radius:10px;color:{EmailPalette.Purple};font:700 30px/40px {Font};letter-spacing:12px;direction:ltr;white-space:nowrap;"><span role="img" aria-label="{spoken}">{code.Digits}</span></td></tr></table>

                        """);
                    break;

                case EmailButton button:
                    html.Append(CultureInfo.InvariantCulture, $"""
                        <table role="presentation" align="center" cellpadding="0" cellspacing="0" border="0" style="margin:26px auto 0;"><tr><td class="e-button" data-part="button" align="center" style="background:{EmailPalette.Purple};border-radius:8px;"><a href="{Encode(button.Address)}" style="display:inline-block;min-width:136px;padding:14px 32px;color:{EmailPalette.OnPurple};font:600 14px/20px {Font};text-decoration:none;">{Words(button.Label)}</a></td></tr></table>
                        <p class="e-note" data-part="link" style="margin:22px 0 0;font:400 11px/18px {Font};color:{EmailPalette.Note};overflow-wrap:anywhere;word-break:break-word;">{Encode(CopyWords)} <a class="e-note" href="{Encode(button.Address)}" dir="ltr" style="color:{EmailPalette.Note};text-decoration:none;unicode-bidi:isolate;">{Encode(button.Address)}</a></p>

                        """);
                    break;

                case EmailFacts facts:
                    DrawFacts(facts, html);
                    break;

                case EmailAttention attention:
                    DrawAttention(attention, html);
                    break;

                default:
                    throw new EmailCompositionException("Not a part of an email.");
            }
        }

        private void DrawFacts(EmailFacts facts, StringBuilder html)
        {
            html.Append(CultureInfo.InvariantCulture, $"""
                <table role="presentation" class="e-box" data-part="facts" align="center" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:24px auto 0;max-width:440px;border:1px solid {EmailPalette.Rule};border-radius:10px;border-collapse:separate;"><tr><td style="padding:9px 20px;">
                <table role="presentation" dir="{Direction}" width="100%" cellpadding="0" cellspacing="0" border="0">

                """);

            foreach (var row in facts.Rows)
            {
                FactRow(row, "400", "600", html);
            }

            if (facts.Total is { } plain && !facts.Ruled)
            {
                // Frame 2227:237: the last row with only its value standing out.
                FactRow(plain, "400", "700", html, standsOut: true, quietName: true);
            }
            else if (facts.Total is { } total)
            {
                html.Append(CultureInfo.InvariantCulture, $"""
                    <tr><td colspan="2" style="padding:7px 0;"><div class="e-rule" style="border-top:1px solid {EmailPalette.Rule};font-size:0;line-height:0;">&nbsp;</div></td></tr>

                    """);
                FactRow(total, "600", "700", html, standsOut: true);
            }

            if (facts.Status is { } status)
            {
                html.Append(CultureInfo.InvariantCulture, $"""
                    <tr><td data-part="status" style="padding:6px 0 5px;text-align:{Start};">{Pill(status)}</td><td class="e-note" style="padding:6px 0 5px;text-align:{End};font:400 11px/16px {Font};color:{EmailPalette.Note};">{(status.Beside is null ? "" : Words(status.Beside))}</td></tr>

                    """);
            }

            html.Append("""
                </table>
                </td></tr></table>

                """);
        }

        private void FactRow(
            EmailFact fact, string nameWeight, string valueWeight, StringBuilder html, bool standsOut = false, bool quietName = false)
        {
            var nameColour = standsOut && !quietName ? EmailPalette.Title : EmailPalette.Body;
            var nameClass = standsOut && !quietName ? "e-title" : "e-body";
            html.Append(CultureInfo.InvariantCulture, $"""
                <tr data-part="fact"{(standsOut ? " data-total=\"true\"" : "")}><td class="{nameClass}" style="padding:5px 0;text-align:{Start};vertical-align:top;font:{nameWeight} 13px/24px {Font};color:{nameColour};">{Words(fact.Name)}</td><td class="e-title" style="padding:{(english ? "5px 0 5px 12px" : "5px 12px 5px 0")};text-align:{End};vertical-align:top;font:{valueWeight} 13px/24px {Font};color:{EmailPalette.Title};overflow-wrap:anywhere;">{Words(fact.Value)}</td></tr>

                """);
        }

        private void DrawAttention(EmailAttention attention, StringBuilder html)
        {
            html.Append(CultureInfo.InvariantCulture, $"""
                <table role="presentation" class="e-attention-{attention.Tone.ToString().ToLowerInvariant()}" data-part="attention" align="center" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:24px auto 0;max-width:400px;background:{EmailPalette.Attention(attention.Tone)};border-radius:8px;"><tr><td style="padding:16px;text-align:{Start};">

                """);

            if (attention.Mark is { } mark)
            {
                html.Append(CultureInfo.InvariantCulture, $"""
                    <p style="margin:0 0 8px;">{Pill(mark)}</p>

                    """);
            }

            html.Append(CultureInfo.InvariantCulture, $"""
                <p class="e-title" style="margin:0;font:600 13px/20px {Font};color:{EmailPalette.Title};">{Words(attention.Line)}</p>

                """);

            if (attention.Second is { } second)
            {
                html.Append(CultureInfo.InvariantCulture, $"""
                    <p class="e-body" style="margin:2px 0 0;font:400 12px/18px {Font};color:{EmailPalette.Body};">{Words(second)}</p>

                    """);
            }

            html.Append("""
                </td></tr></table>

                """);
        }

        private string Pill(EmailStatus status)
        {
            var (ground, text) = EmailPalette.Mark(status.Tone);
            return $"""<span data-part="mark-{status.Tone.ToString().ToLowerInvariant()}" style="display:inline-block;padding:3px 12px;border-radius:11px;background:{ground};color:{text};font:700 11px/16px {Font};">{Words(status.Text)}</span>""";
        }

        private static string RuleRow() =>
            $"""<tr><td class="e-rule" data-part="rule" style="border-top:1px solid {EmailPalette.Rule};font-size:0;line-height:0;height:0;">&nbsp;</td></tr>""";

        /// <summary>
        /// Added for mail programs that ask for a dark theme; marks and status words keep their
        /// own light grounds, which stay readable on a dark card.
        /// </summary>
        private static string DarkTheme()
        {
            var rules = new StringBuilder();
            rules.Append(CultureInfo.InvariantCulture, $$"""
                @media (prefers-color-scheme: dark) {
                  .e-ground { background: {{EmailPalette.DarkGround}} !important; }
                  .e-card { background: {{EmailPalette.DarkCard}} !important; }
                  .e-rule { border-color: {{EmailPalette.DarkRule}} !important; }
                  .e-title { color: {{EmailPalette.DarkTitle}} !important; }
                  .e-body { color: {{EmailPalette.DarkBody}} !important; }
                  .e-note { color: {{EmailPalette.DarkNote}} !important; }
                  .e-purple { color: {{EmailPalette.DarkPurpleText}} !important; }
                  .e-code { background: {{EmailPalette.DarkCodeGround}} !important; border-color: {{EmailPalette.DarkRule}} !important; color: {{EmailPalette.DarkCodeText}} !important; }
                  .e-box { background: {{EmailPalette.DarkBox}} !important; border-color: {{EmailPalette.DarkRule}} !important; }

                """);

            foreach (var tone in Enum.GetValues<EmailTone>())
            {
                rules.Append(CultureInfo.InvariantCulture, $$"""
                      .e-attention-{{tone.ToString().ToLowerInvariant()}} { background: {{EmailPalette.DarkAttention(tone)}} !important; }

                    """);
            }

            rules.Append('}');
            return rules.ToString();
        }

        // ------------------------------------------------------------------ the text-only form

        public string Text()
        {
            var lines = new List<string> { Plain(content.Eyebrow), Plain(content.Title), "" };

            foreach (var part in content.Parts.Where(part => !part.IsEmpty))
            {
                switch (part)
                {
                    case EmailParagraph paragraph:
                        lines.Add(Plain(paragraph.Text));
                        lines.Add("");
                        break;
                    case EmailNote note:
                        lines.Add(Plain(note.Text));
                        lines.Add("");
                        break;
                    case EmailCode code:
                        lines.Add(code.Digits);
                        lines.Add("");
                        break;
                    case EmailButton button:
                        lines.Add($"{Plain(button.Label)}:");
                        lines.Add(button.Address);
                        lines.Add("");
                        break;
                    case EmailFacts facts:
                        lines.AddRange(facts.Rows.Select(row => $"{Plain(row.Name)}: {Plain(row.Value)}"));
                        if (facts.Total is { } total)
                        {
                            lines.Add($"{Plain(total.Name)}: {Plain(total.Value)}");
                        }

                        if (facts.Status is { } status)
                        {
                            lines.Add(status.Beside is null ? Plain(status.Text) : $"{Plain(status.Text)} — {Plain(status.Beside)}");
                        }

                        lines.Add("");
                        break;
                    case EmailAttention attention:
                        if (attention.Mark is { } mark)
                        {
                            lines.Add(Plain(mark.Text));
                        }

                        lines.Add(Plain(attention.Line));
                        if (attention.Second is { } second)
                        {
                            lines.Add(Plain(second));
                        }

                        lines.Add("");
                        break;
                    default:
                        throw new EmailCompositionException("Not a part of an email.");
                }
            }

            lines.Add("--");
            lines.Add(Name);

            var address = platform.Address(content.Language);
            if (address.Length > 0)
            {
                lines.Add(address);
            }

            var phone = platform.Phone();
            lines.Add(phone.Length > 0 ? $"{platform.Support()} · {phone}" : platform.Support());
            lines.Add(Plain(Reason));

            return string.Join('\n', lines) + "\n";
        }

        // ------------------------------------------------------------------ words

        /// <summary>The line a mail program shows beside the subject: the email's first words of substance.</summary>
        private string Preview() =>
            content.Parts.OfType<EmailParagraph>().FirstOrDefault(paragraph => !paragraph.IsEmpty)?.Text ?? content.Title;

        private static string Encode(string value) => WebUtility.HtmlEncode(value);

        /// <summary>
        /// Words as they go on the page: the platform's name in place of its token, everything
        /// encoded, and a value marked left to right kept in its own order.
        /// </summary>
        private string Words(string text)
        {
            var encoded = Encode(text.Replace(EmailContent.Platform, Name, StringComparison.Ordinal));
            var opens = encoded.Count(c => c == EmailMarks.Open);
            var balanced = opens == encoded.Count(c => c == EmailMarks.Close);

            if (!balanced || opens == 0)
            {
                return Unmarked(encoded);
            }

            // A short value — a number of an invoice, a ticket — is not broken over two lines;
            // a long one, an address, breaks where it must.
            var page = new StringBuilder(encoded.Length + (opens * 64));
            var at = 0;
            while (at < encoded.Length)
            {
                var open = encoded.IndexOf(EmailMarks.Open, at);
                var close = open < 0 ? -1 : encoded.IndexOf(EmailMarks.Close, open);
                if (close < 0)
                {
                    page.Append(Unmarked(encoded[at..]));
                    break;
                }

                var value = Unmarked(encoded[(open + 1)..close]);
                page.Append(Unmarked(encoded[at..open]))
                    .Append("<span dir=\"ltr\" style=\"unicode-bidi:isolate;")
                    .Append(value.Length <= KeptWhole ? "white-space:nowrap;" : "")
                    .Append("\">")
                    .Append(value)
                    .Append("</span>");
                at = close + 1;
            }

            return page.ToString();
        }

        /// <summary>The longest left-to-right value that is kept on one line.</summary>
        private const int KeptWhole = 24;

        private string Plain(string text) =>
            Unmarked(text.Replace(EmailContent.Platform, Name, StringComparison.Ordinal));

        private static string Unmarked(string text) =>
            text.Replace(EmailMarks.Open.ToString(), "", StringComparison.Ordinal)
                .Replace(EmailMarks.Close.ToString(), "", StringComparison.Ordinal);
    }
}
