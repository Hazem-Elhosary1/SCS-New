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

    /// <summary>The longest derived base: room is left for <c>-NN</c> on a clash (spec 015, research R9).</summary>
    public const int DerivedMaxLength = 33;

    /// <summary>
    /// The slug of a new clinic, from the Latin letters and digits of its English name, or of its
    /// Arabic name when it has no English one (spec 015, FR-004; BRD Ops §3-1): lower case, runs
    /// joined by dashes, starting with a letter, at most 33 characters. A result that is too
    /// short, reserved or empty becomes <c>clinic-</c> and six random letters and digits.
    /// </summary>
    public static string Derive(string? englishName, string? arabicName, Func<int, int>? random = null)
    {
        var source = string.IsNullOrWhiteSpace(englishName) ? arabicName : englishName;
        var runs = LatinRuns().Matches((source ?? "").ToLowerInvariant()).Select(m => m.Value);
        var joined = string.Join('-', runs).TrimStart('-', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

        if (joined.Length > DerivedMaxLength)
        {
            joined = joined[..DerivedMaxLength].TrimEnd('-');
        }

        return TryCreate(joined, out var canonical, out _) ? canonical : Fallback(random ?? Random.Shared.Next);
    }

    /// <summary>The <paramref name="number"/>th candidate of a base: the base, then <c>-2</c>, <c>-3</c> and so on.</summary>
    public static string Candidate(string derived, int number) => number <= 1 ? derived : $"{derived}-{number}";

    private static string Fallback(Func<int, int> random)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        var tail = new char[6];
        for (var i = 0; i < tail.Length; i++)
        {
            tail[i] = alphabet[random(alphabet.Length)];
        }

        return "clinic-" + new string(tail);
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    [GeneratedRegex("[a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex LatinRuns();
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
