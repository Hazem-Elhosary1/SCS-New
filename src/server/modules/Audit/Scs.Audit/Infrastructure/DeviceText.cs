namespace Scs.Audit.Infrastructure;

/// <summary>
/// Describes a device from the user agent an entry stored (spec 007, FR-038): the browser's
/// family and the operating system, as the design's log writes it — «متصفح Chrome على Windows».
/// The raw text stays on the entry, so an old entry reads better when this learns a new browser.
/// </summary>
internal static class DeviceText
{
    public static string Describe(string? userAgent, string language)
    {
        var english = language == Sentences.English;
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
