using System.Text.RegularExpressions;

namespace Scs.SharedKernel.Clinics;

/// <summary>
/// The rules of the clinic form's fields that more than one module checks (spec 015, FR-003;
/// data-model.md §7): the module that adds a clinic checks them all before anything is written,
/// and each owning module checks its own again when it writes.
/// </summary>
public static partial class ClinicFields
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 300;
    public const int EmailMaxLength = 254;
    public const int NoteMaxLength = 500;

    public const string Required = "REQUIRED";
    public const string TooLong = "TOO_LONG";
    public const string Invalid = "INVALID";
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string TaxNumberCode = "TAX_NUMBER";
    public const string MobileCode = "MOBILE";
    public const string Taken = "TAKEN";

    public static IReadOnlyList<string> Languages { get; } = ["ar", "en"];

    /// <summary>
    /// Nine digits, typed with or without dashes and spaces, kept as <c>NNN-NNN-NNN</c> (BRD Ops
    /// §3-1). Null and empty are "none"; anything else that is not nine digits is refused.
    /// </summary>
    public static bool TryTaxNumber(string? typed, out string? kept)
    {
        kept = null;
        if (string.IsNullOrWhiteSpace(typed))
        {
            return true;
        }

        var digits = typed.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        if (!NineDigits().IsMatch(digits))
        {
            return false;
        }

        kept = $"{digits[..3]}-{digits[3..6]}-{digits[6..]}";
        return true;
    }

    /// <summary>
    /// A phone number from any country (the owner's answer of 17 Oct 2026): an optional leading
    /// <c>+</c>, then 8 to 15 digits; spaces and dashes are ignored and not kept.
    /// </summary>
    public static bool TryMobile(string? typed, out string kept)
    {
        kept = (typed ?? "").Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return Mobile().IsMatch(kept);
    }

    /// <summary>
    /// The address's shape only, as accounts check it (spec 003): one <c>@</c>, text on both sides,
    /// a dot in the domain. Whether it has an account is the owner's module's question.
    /// </summary>
    public static bool IsEmail(string? email)
    {
        var value = CanonicalEmail(email);
        var at = value.IndexOf('@', StringComparison.Ordinal);
        var domain = at < 0 ? "" : value[(at + 1)..];
        return value.Length is > 0 and <= EmailMaxLength
               && at > 0
               && at == value.LastIndexOf('@')
               && domain.Length >= 3
               && domain.Contains('.', StringComparison.Ordinal)
               && !domain.StartsWith('.')
               && !domain.EndsWith('.')
               && !domain.Contains("..", StringComparison.Ordinal)
               && !value.Any(char.IsWhiteSpace)
               && !value.Any(char.IsControl);
    }

    /// <summary>Trimmed and lower case, as accounts keep it.</summary>
    public static string CanonicalEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    // Western digits only: «\d» would take Eastern-Arabic ones, which the columns refuse (T076).
    [GeneratedRegex(@"^[0-9]{9}$")]
    private static partial Regex NineDigits();

    [GeneratedRegex(@"^\+?[0-9]{8,15}$")]
    private static partial Regex Mobile();
}
