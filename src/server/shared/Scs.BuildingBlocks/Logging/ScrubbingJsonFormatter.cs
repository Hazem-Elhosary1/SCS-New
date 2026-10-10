using System.Text.RegularExpressions;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

namespace Scs.BuildingBlocks.Logging;

/// <summary>
/// The last line of defence for FR-021. Every log line, whatever produced it — message,
/// property or exception text — is written as compact JSON and then has anything shaped like
/// personal data or a secret masked: an email address, an Egyptian mobile number, a 14-digit
/// national ID, a bearer or JWT token, a connection-string credential.
///
/// It cannot recognise a person's name in free text. Names are kept out by the layers before
/// this one: request data is never logged, and classified members are masked.
/// </summary>
public sealed partial class ScrubbingJsonFormatter : ITextFormatter
{
    public const string Mask = "***";

    private readonly CompactJsonFormatter _inner = new();

    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        using var buffer = new StringWriter();
        _inner.Format(logEvent, buffer);
        output.Write(Scrub(buffer.ToString()));
    }

    public static string Scrub(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return Sensitive().Replace(line, match =>
        {
            // Request references and timestamps are hexadecimal or numeric and must survive.
            if (match.Groups["keep"].Success)
            {
                return match.Value;
            }

            if (match.Groups["credentialKey"].Success)
            {
                return match.Groups["credentialKey"].Value + "=" + Mask;
            }

            return match.Groups["bearer"].Success ? "Bearer " + Mask : Mask;
        });
    }

    [GeneratedRegex(
        """
        (?<keep>"(?:@tr|@sp|@t|@i|traceId|clientTraceId)":"[^"]*")
        |(?<bearer>Bearer\s+[A-Za-z0-9._~+/=-]{8,})
        |eyJ[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]{4,}\.[A-Za-z0-9_-]*
        |(?<credentialKey>(?:password|pwd|user\s?id|uid|accountkey|sharedaccesskey))\s*=\s*[^;"\\\s]+
        |[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+
        |(?<!\d)(?:\+|00)?20\s?1[0125]\d{8}(?!\d)
        |(?<!\d)01[0125]\d{8}(?!\d)
        |(?<!\d)[23]\d{13}(?!\d)
        """,
        RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]
    private static partial Regex Sensitive();
}
