using Scs.SharedKernel;

namespace Scs.Identity.Domain;

/// <summary>The choices a preference can hold.</summary>
public static class PreferenceValues
{
    public static IReadOnlyList<string> Languages { get; } = ["ar", "en"];

    /// <summary>The subscription banners a person may close for a day. The read-only one cannot be closed (CL-SH-038).</summary>
    public static IReadOnlyList<string> ClosableBanners { get; } = ["trial", "expiring", "grace"];
}

/// <summary>
/// What one person keeps between sessions and across browsers (spec 006, أ-ق25): the language
/// of the console, whether the side bar is folded, and a subscription banner closed for a day.
/// One row per account, created by the first choice. Clinic-owned.
/// </summary>
public sealed class StaffPreference : IClinicOwned
{
    private StaffPreference()
    {
    }

    public StaffPreference(Guid accountId) => AccountId = accountId;

    public Guid AccountId { get; private set; }

    public Guid ClinicId { get; private set; }

    /// <summary><c>ar</c> or <c>en</c>; null while the person never chose.</summary>
    public string? Language { get; private set; }

    /// <summary>Null while the person never chose: the width of the screen then decides.</summary>
    public bool? SidebarFolded { get; private set; }

    /// <summary>The banner state the person closed, and the Cairo day they closed it on. It counts on that day only.</summary>
    public string? DismissedBanner { get; private set; }

    public DateOnly? DismissedOnDay { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Choose(string? language, bool? sidebarFolded, string? dismissedBanner, DateOnly today, DateTimeOffset now)
    {
        if (language is not null)
        {
            Language = language;
        }

        if (sidebarFolded is not null)
        {
            SidebarFolded = sidebarFolded;
        }

        if (dismissedBanner is not null)
        {
            DismissedBanner = dismissedBanner;
            DismissedOnDay = today;
        }

        UpdatedAtUtc = now;
    }

    /// <summary>The banner the person closed, if they closed it today.</summary>
    public string? BannerClosedOn(DateOnly today) => DismissedOnDay == today ? DismissedBanner : null;
}
