namespace Scs.SharedKernel.Plans;

/// <summary>
/// The keys of the feature catalogue (spec 012) that code refers to by name: a route that belongs
/// to a feature, a limit a module counts (spec 014). What each key includes, and every number, is
/// in the database (SH-010, SH-020); only the key is fixed. A test holds every constant to the
/// catalogue's seed.
/// </summary>
public static class FeatureKeys
{
    public const string CoreClinic = "core_clinic";
    public const string MaxUsers = "max_users";
    public const string MaxDoctors = "max_doctors";
    public const string MaxBranches = "max_branches";
    public const string QueueScreen = "queue_screen";
    public const string MessageCredits = "message_credits";
    public const string TeleconsultMinutes = "teleconsult_minutes";
    public const string AiXrayAnalysis = "ai_xray_analysis";
    public const string AiVoiceNotes = "ai_voice_notes";
    public const string AllowWhatsapp = "allow_whatsapp";
    public const string AllowTelegram = "allow_telegram";
    public const string AllowAnalytics = "allow_analytics";
    public const string SpecialtyExamModules = "specialty_exam_modules";

    /// <summary>The eight counted limits, in the BRD's order (SH-015).</summary>
    public static IReadOnlyList<string> CountedLimits { get; } =
    [
        MaxUsers, MaxDoctors, MaxBranches, MessageCredits, QueueScreen, TeleconsultMinutes, AiXrayAnalysis, AiVoiceNotes,
    ];

    /// <summary>The four limits counted per month, from the meter (SH-018, SH-019).</summary>
    public static IReadOnlyList<string> Monthly { get; } = [MessageCredits, TeleconsultMinutes, AiXrayAnalysis, AiVoiceNotes];

    public static bool IsMonthly(string key) => Monthly.Contains(key, StringComparer.Ordinal);
}
