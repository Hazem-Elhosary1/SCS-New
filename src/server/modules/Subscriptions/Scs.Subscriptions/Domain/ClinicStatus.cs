namespace Scs.Subscriptions.Domain;

/// <summary>A clinic's status as the operators see it: a code, its words and the badge's tone.</summary>
public sealed record ClinicStatusView(string Code, string Ar, string En, string Tone);

/// <summary>
/// The status shown for a clinic (spec 015, FR-015; data-model.md §3): «تجربة» until its trial
/// ends, «نشطة» after. Spec 016 adds the other statuses and what moves a clinic between them; the
/// stored states it will read are passed through untouched until then.
/// </summary>
public static class ClinicStatus
{
    public const string Trial = "trial";
    public const string Active = "active";

    public static ClinicStatusView Of(string storedState, DateTimeOffset? trialEndsAtUtc, DateTimeOffset now) =>
        storedState switch
        {
            "active" when trialEndsAtUtc is { } end && end > now => new ClinicStatusView(Trial, "تجربة", "Trial", "info"),
            "active" => new ClinicStatusView(Active, "نشطة", "Active", "success"),

            // Not reached before spec 016 gives these their words; never shown as active meanwhile.
            _ => new ClinicStatusView(storedState, storedState, storedState, "neutral"),
        };

    /// <summary>Whole days left of a trial, counted up: a trial ending in 3 days 2 hours has 4 days left.</summary>
    public static int DaysLeft(DateTimeOffset trialEndsAtUtc, DateTimeOffset now) =>
        trialEndsAtUtc <= now ? 0 : (int)Math.Ceiling((trialEndsAtUtc - now).TotalDays);
}
