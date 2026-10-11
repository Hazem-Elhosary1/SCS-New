using System.Globalization;

namespace Scs.SharedKernel.Wording;

/// <summary>
/// What every part of the platform that turns stored facts into words shares (specs 007 and
/// 008): the two languages, and how each writes a number.
/// </summary>
public static class Words
{
    public const string Arabic = "ar";
    public const string English = "en";

    /// <summary><c>en</c> stays English; anything else is Arabic, the platform's first language.</summary>
    public static string Language(string? language) => language == English ? English : Arabic;

    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    private static readonly string[] MonthsAr =
        ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"];

    private static readonly string[] MonthsEn =
        ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>
    /// A moment in Cairo time, where every clinic's day begins and ends (SH-001), as the design
    /// writes it: «١١ أكتوبر ٢٠٢٦، ٣:٠٤ م» · "11 October 2026, 3:04 PM".
    /// </summary>
    public static string Moment(DateTimeOffset moment, bool english)
    {
        var local = TimeZoneInfo.ConvertTime(moment, Cairo);
        var hour = local.Hour % 12 == 0 ? 12 : local.Hour % 12;
        var minute = local.Minute.ToString("00", CultureInfo.InvariantCulture);
        var morning = local.Hour < 12;

        return english
            ? $"{local.Day} {MonthsEn[local.Month - 1]} {local.Year}, {hour}:{minute} {(morning ? "AM" : "PM")}"
            : $"{Digits(local.Day)} {MonthsAr[local.Month - 1]} {Digits(local.Year)}، {Digits(hour)}:{Digits(minute)} {(morning ? "ص" : "م")}";

        static string Digits(object value) =>
            string.Concat(Convert.ToString(value, CultureInfo.InvariantCulture)!.Select(digit => (char)('٠' + (digit - '0'))));
    }

    /// <summary>A whole number as the reader's language writes it: Arabic-Indic digits in Arabic.</summary>
    public static string Number(int value, bool english)
    {
        var latin = value.ToString(CultureInfo.InvariantCulture);
        return english ? latin : string.Concat(latin.Select(digit => char.IsAsciiDigit(digit) ? (char)('٠' + (digit - '0')) : digit));
    }
}

/// <summary>
/// Describes a device from a stored user agent: the browser's family and the operating system,
/// as the design writes it — «متصفح Chrome على Windows». The raw text stays where it was
/// stored, so an old record reads better when this learns a new browser.
/// </summary>
public static class DeviceText
{
    public static string Describe(string? userAgent, string language)
    {
        var english = language == Words.English;
        var browser = userAgent is null ? null : Browser(userAgent);
        var system = userAgent is null ? null : System(userAgent);

        return (browser, system) switch
        {
            (null, null) => english ? "Unknown device" : "جهاز غير معروف",
            (not null, null) => english ? browser : $"متصفح {browser}",
            (null, not null) => english ? $"A browser on {system}" : $"متصفح على {system}",
            _ => english ? $"{browser} on {system}" : $"متصفح {browser} على {system}",
        };
    }

    // The order matters: Edge and Opera also say "Chrome", Chrome also says "Safari".
    private static string? Browser(string agent) =>
        Has(agent, "Edg/") || Has(agent, "EdgA/") || Has(agent, "EdgiOS/") ? "Edge"
        : Has(agent, "OPR/") || Has(agent, "Opera") ? "Opera"
        : Has(agent, "Firefox/") || Has(agent, "FxiOS/") ? "Firefox"
        : Has(agent, "Chrome/") || Has(agent, "CriOS/") ? "Chrome"
        : Has(agent, "Safari/") ? "Safari"
        : null;

    // iPhone and iPad say "like Mac OS X", Android says "Linux".
    private static string? System(string agent) =>
        Has(agent, "iPhone") || Has(agent, "iPad") || Has(agent, "iPod") ? "iOS"
        : Has(agent, "Android") ? "Android"
        : Has(agent, "Windows") ? "Windows"
        : Has(agent, "Mac OS X") || Has(agent, "Macintosh") ? "macOS"
        : Has(agent, "CrOS") ? "ChromeOS"
        : Has(agent, "Linux") ? "Linux"
        : null;

    private static bool Has(string agent, string part) => agent.Contains(part, StringComparison.Ordinal);
}
