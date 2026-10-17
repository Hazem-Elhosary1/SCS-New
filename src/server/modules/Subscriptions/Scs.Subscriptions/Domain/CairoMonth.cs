using System.Globalization;

namespace Scs.Subscriptions.Domain;

/// <summary>
/// The month a moment falls in, in Cairo (SH-001; spec 014, FR-022): a month runs from 00:00 on
/// its 1st to the end of its last day there, whatever the clock's zone. Worked out on every write
/// and read, so nothing is reset at midnight.
/// </summary>
public static class CairoMonth
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    /// <summary><c>yyyy-MM</c> of the moment in Cairo.</summary>
    public static string Of(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, Cairo).ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>The 1st of the month after the moment's, in Cairo: when the allowance renews.</summary>
    public static DateOnly RenewsOn(DateTimeOffset moment)
    {
        var local = TimeZoneInfo.ConvertTime(moment, Cairo);
        return new DateOnly(local.Year, local.Month, 1).AddMonths(1);
    }

    /// <summary>Whether the text is a month this type would write.</summary>
    public static bool IsMonth(string? value) =>
        value is { Length: 7 }
        && DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
