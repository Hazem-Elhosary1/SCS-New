namespace Scs.Plans.Contracts;

/// <summary>
/// One feature of the catalogue, as another module reads it (spec 012, FR-025): what it is, its
/// specialty and its baseline. The plans (013) and their enforcement (014) read it and never keep
/// a copy of their own.
/// </summary>
/// <param name="Kind"><c>switch</c> or <c>limit</c>.</param>
/// <param name="Baseline">What a clinic has when its plan leaves the limit out; null for no limit.</param>
/// <param name="Specialty">One of the fifteen specialties' codes, or null for every specialty.</param>
/// <param name="Status"><c>built</c> or <c>upcoming</c>.</param>
/// <param name="Monthly">Counted each month: from a fixed list of four, never a field.</param>
public sealed record CatalogueFeature(
    string Key,
    string NameAr,
    string NameEn,
    string Kind,
    string? UnitAr,
    string? UnitEn,
    int? Baseline,
    string? Specialty,
    string Status,
    bool IsSold,
    bool IsRetired,
    bool Monthly);

/// <summary>The catalogue's one read for other modules. In-process only; there is no address for it.</summary>
public interface IFeatureCatalogue
{
    /// <summary>The feature of this key, retired or not; null for a key the catalogue does not hold — never a feature (FR-026).</summary>
    Task<CatalogueFeature?> FindAsync(string key, CancellationToken cancellation);

    /// <summary>What a plan may take: the sold features that are not retired, in the catalogue's order (FR-021).</summary>
    Task<IReadOnlyList<CatalogueFeature>> SellableAsync(CancellationToken cancellation);
}
