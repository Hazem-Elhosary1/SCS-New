using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Mail;

namespace Scs.UnitTests.Mail;

/// <summary>
/// Spec 094, FR-037: the catalogue of every email the platform can send. It is gathered from
/// what each module provides and from the template's own samples, and — when
/// <c>SCS_EMAILS_OUT</c> names a folder — written there as pages: each email exactly as it
/// would be sent, its text-only form, and an index. Nothing of the platform serves it.
/// </summary>
public class EmailCatalogueExport
{
    public const string OutVariable = "SCS_EMAILS_OUT";

    /// <summary>Every sample of the template and of the modules that send email.</summary>
    public static IReadOnlyList<EmailSample> AllSamples() =>
    [
        .. Assemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IEmailSamples).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .SelectMany(type => ((IEmailSamples)Activator.CreateInstance(type, nonPublic: true)!).All()),
    ];

    /// <summary>The assemblies that may build an email: the three modules that send, and these tests.</summary>
    public static IReadOnlyList<Assembly> Assemblies() =>
    [
        typeof(SampleKinds).Assembly,
        typeof(Scs.Identity.IdentityModule).Assembly,
        typeof(Scs.Notifications.NotificationsModule).Assembly,
        typeof(Scs.Operators.OperatorsModule).Assembly,
    ];

    /// <summary>The identity the catalogue is shown with: everything set, with the frames' sample values.</summary>
    public static PlatformOptions Identity => EmailRendererTests.Full;

    [Fact]
    public void The_catalogue_is_written_as_pages_when_a_folder_is_named()
    {
        var folder = Environment.GetEnvironmentVariable(OutVariable);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        foreach (var stale in Directory.EnumerateFiles(folder))
        {
            File.Delete(stale);
        }

        var entries = new List<object>();
        var index = new StringBuilder(
            "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Emails</title></head>" +
            "<body style=\"font:14px/1.6 'Segoe UI',Arial,sans-serif;margin:32px;color:#1a2024\">" +
            "<h1>Every email the platform can send</h1><p>Sample values; nothing here was sent.</p>");
        var audience = "";

        foreach (var sample in AllSamples().OrderBy(s => s.Audience, StringComparer.Ordinal).ThenBy(s => s.Slug, StringComparer.Ordinal))
        {
            var message = EmailRenderer.Render(sample.Content, Identity);
            File.WriteAllText(Path.Combine(folder, $"{sample.Slug}.html"), message.HtmlBody, Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, $"{sample.Slug}.txt"), message.TextBody, Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, $"{sample.Slug}.text.html"), TextPage(sample, message), Encoding.UTF8);

            if (sample.Audience != audience)
            {
                audience = sample.Audience;
                index.Append(System.Globalization.CultureInfo.InvariantCulture, $"<h2>{WebUtility.HtmlEncode(audience)}</h2>");
            }

            index.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"<p><a href=\"{sample.Slug}.html\">{WebUtility.HtmlEncode(sample.Slug)}</a> · " +
                $"<a href=\"{sample.Slug}.text.html\">text only</a> — " +
                $"<span dir=\"auto\">{WebUtility.HtmlEncode(message.SenderName)}: {WebUtility.HtmlEncode(message.Subject)}</span></p>");

            entries.Add(new
            {
                slug = sample.Slug,
                kind = sample.Kind,
                audience = sample.Audience,
                variant = sample.Variant,
                language = sample.Language,
                subject = message.Subject,
                sender = message.SenderName,
                hasButton = sample.Content.Parts.OfType<EmailButton>().Any(),
                hasCode = sample.Content.Parts.OfType<EmailCode>().Any(),
            });
        }

        // The shell again with only what a deployment must set: no phone, no address.
        var bare = new PlatformOptions { NameAr = Identity.NameAr, NameEn = Identity.NameEn, SupportEmail = Identity.SupportEmail };
        foreach (var language in new[] { "ar", "en" })
        {
            var slug = $"sample-shell-bare-{language}";
            var message = EmailRenderer.Render(SampleKinds.Shell(language), bare);
            File.WriteAllText(Path.Combine(folder, $"{slug}.html"), message.HtmlBody, Encoding.UTF8);
            index.Append(System.Globalization.CultureInfo.InvariantCulture, $"<p><a href=\"{slug}.html\">{slug}</a> — the footer with no phone and no address</p>");
        }

        index.Append("</body></html>");
        File.WriteAllText(Path.Combine(folder, "index.html"), index.ToString(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(folder, "catalogue.json"), JsonSerializer.Serialize(entries), Encoding.UTF8);
    }

    private static string TextPage(EmailSample sample, EmailMessage message) =>
        $"<!doctype html><html lang=\"{sample.Language}\" dir=\"{(sample.Language == "en" ? "ltr" : "rtl")}\"><head><meta charset=\"utf-8\">" +
        $"<title>{WebUtility.HtmlEncode(message.Subject)}</title></head><body style=\"margin:24px\">" +
        $"<pre data-part=\"text\" style=\"font:14px/1.6 Consolas,monospace;white-space:pre-wrap;overflow-wrap:anywhere\">{WebUtility.HtmlEncode(message.TextBody)}</pre></body></html>";
}
