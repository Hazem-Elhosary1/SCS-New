namespace Scs.Subscriptions.Contracts;

/// <summary>
/// The eight counted limits of the established clinic, with the same names and numbers wherever
/// they are shown (spec 014, SH-016): the clinic's settings (024), «الاستخدام» in the profile and
/// subscription (078), the clinic's detail for operators (015).
/// </summary>
public interface IClinicLimits
{
    /// <summary>The eight in the BRD's order (SH-015).</summary>
    Task<IReadOnlyList<LimitReading>> ReadAsync(CancellationToken cancellation);
}

/// <param name="Limit">Null for no limit.</param>
/// <param name="Used">Null when nothing counts it yet — never shown as 0 then.</param>
/// <param name="RenewsOn">For a monthly limit: the 1st of next month, in Cairo.</param>
public sealed record LimitReading(
    string Key, string NameAr, string NameEn, string? UnitAr, string? UnitEn,
    int? Limit, int? Used, bool Monthly, DateOnly? RenewsOn);

/// <summary>
/// How many of a standing limit's records the established clinic holds, registered by the module
/// that owns them (Identity counts <c>max_users</c>; 028 branches, 029 doctors). What "counted"
/// means is the owner's (spec 014, FR-014).
/// </summary>
public interface ILimitCounter
{
    string FeatureKey { get; }

    Task<int> CountAsync(CancellationToken cancellation);
}
