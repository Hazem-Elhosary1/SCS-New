namespace Scs.Identity.Domain;

/// <summary>
/// An email address in the one form it is stored and compared in: trimmed and lower case
/// (spec 003, FR-002). The check is deliberately modest — one <c>@</c>, text on both sides, a
/// dot in the domain — because the only real proof of an address is a message arriving at it.
/// </summary>
public sealed record EmailAddress
{
    public const int MaxLength = 254;

    public const string Required = "REQUIRED";
    public const string TooLong = "TOO_LONG";
    public const string InvalidFormat = "INVALID_FORMAT";

    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    /// <summary>The stored form of whatever was typed, valid or not. Used to look an account up.</summary>
    public static string Normalize(string? text) => (text ?? "").Trim().ToLowerInvariant();

    /// <summary>Returns the address, or the field code of what is wrong with it.</summary>
    public static (EmailAddress? Email, string? Error) TryCreate(string? text)
    {
        var value = Normalize(text);

        if (value.Length == 0)
        {
            return (null, Required);
        }

        if (value.Length > MaxLength)
        {
            return (null, TooLong);
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        var domain = at < 0 ? "" : value[(at + 1)..];
        var wellFormed =
            at > 0
            && at == value.LastIndexOf('@')
            && domain.Length >= 3
            && domain.Contains('.', StringComparison.Ordinal)
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !domain.Contains("..", StringComparison.Ordinal)
            && !value.Any(char.IsWhiteSpace)
            && !value.Any(char.IsControl);

        return wellFormed ? (new EmailAddress(value), null) : (null, InvalidFormat);
    }

    /// <summary>Never the address: this type is personal data.</summary>
    public override string ToString() => nameof(EmailAddress);
}

/// <summary>
/// The rule every password must meet (BRD B-R16): at least ten characters with an upper-case
/// and a lower-case letter, a digit and a symbol. The server is the guard; the screens that
/// show the four lines are spec 004.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 10;

    /// <summary>Long enough for any passphrase, short enough that hashing it cannot be used to tie the server up.</summary>
    public const int MaxLength = 200;

    public const string TooShort = "TOO_SHORT";
    public const string TooLong = "TOO_LONG";
    public const string NoUpperLower = "NO_UPPER_LOWER";
    public const string NoDigit = "NO_DIGIT";
    public const string NoSymbol = "NO_SYMBOL";

    /// <summary>One code per rule not met; empty when the password is acceptable.</summary>
    public static IReadOnlyList<string> Check(string? password)
    {
        password ??= "";
        var problems = new List<string>();

        if (password.Length < MinLength)
        {
            problems.Add(TooShort);
        }

        if (password.Length > MaxLength)
        {
            problems.Add(TooLong);
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower))
        {
            problems.Add(NoUpperLower);
        }

        if (!password.Any(char.IsAsciiDigit))
        {
            problems.Add(NoDigit);
        }

        if (!password.Any(c => char.IsSymbol(c) || char.IsPunctuation(c)))
        {
            problems.Add(NoSymbol);
        }

        return problems;
    }
}
