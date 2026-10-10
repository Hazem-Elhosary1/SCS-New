using System.Text.RegularExpressions;

namespace Scs.Tenancy.Domain;

/// <summary>
/// The short name that identifies a clinic in its patient address (<c>scs-heta.com/al-noor</c>).
/// English only: lowercase letters, digits and hyphens, 3 to 40 characters, starting with a
/// letter (spec 002, FR-003; confirmed by the product owner on 10 Oct 2026).
/// </summary>
public static partial class ShortName
{
    public const int MinLength = 3;
    public const int MaxLength = 40;

    public const string Required = "REQUIRED";
    public const string TooShort = "TOO_SHORT";
    public const string TooLong = "TOO_LONG";
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string Reserved = "RESERVED";
    public const string Taken = "TAKEN";

    /// <summary>Lower case, with surrounding spaces and slashes removed.</summary>
    public static string Canonicalise(string? raw) =>
        (raw ?? "").Trim().Trim('/').Trim().ToLowerInvariant();

    /// <summary>
    /// The canonical form of an acceptable name, or the field code that says why it is not one.
    /// </summary>
    public static bool TryCreate(string? raw, out string canonical, out string? code)
    {
        canonical = Canonicalise(raw);
        code = canonical.Length switch
        {
            0 => Required,
            < MinLength => TooShort,
            > MaxLength => TooLong,
            _ when !Pattern().IsMatch(canonical) => InvalidFormat,
            _ when ReservedShortNames.Contains(canonical) => Reserved,
            _ => null,
        };

        return code is null;
    }

    /// <summary>
    /// Whether text could be a clinic's name at all. Used when resolving an address, so that a
    /// path that cannot be a name never reaches the database.
    /// </summary>
    public static bool CouldBeAName(string canonical) =>
        canonical.Length is >= MinLength and <= MaxLength && Pattern().IsMatch(canonical);

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

/// <summary>
/// Names no clinic may take, because the system uses them in addresses or because they would
/// mislead. One list; later specs add to it and never remove from it.
/// </summary>
public static class ReservedShortNames
{
    private static readonly HashSet<string> Words = new(StringComparer.Ordinal)
    {
        // Used as the first part of an address by the system itself.
        "api", "app", "assets", "i18n", "fonts", "media", "health", "v1", "config", "version",
        "favicon", "index", "static", "public",

        // Would mislead a patient or a member of staff.
        "admin", "ops", "operator", "operators", "support", "help", "login", "logout", "signin",
        "signup", "account", "www", "mail", "status", "docs", "clinic", "clinics", "patient",
        "patients", "heta", "scs",
    };

    public static IReadOnlyCollection<string> All => Words;

    public static bool Contains(string canonical) => Words.Contains(canonical);
}
