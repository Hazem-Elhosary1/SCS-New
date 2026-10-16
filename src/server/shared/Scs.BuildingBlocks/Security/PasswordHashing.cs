using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Scs.BuildingBlocks.Security;

/// <param name="Matches">The password is the account's.</param>
/// <param name="NeedsRehash">It matched a hash made with older settings; store a new one.</param>
public readonly record struct PasswordVerification(bool Matches, bool NeedsRehash);

/// <summary>
/// Passwords are kept as PBKDF2-HMAC-SHA-512 hashes with a per-password salt, in one versioned
/// text value: <c>v1.&lt;iterations&gt;.&lt;salt&gt;.&lt;hash&gt;</c> (research R5). The version lets the
/// function change later without anyone having to reset a password.
/// </summary>
public static class PasswordHasher
{
    public const string Version = "v1";
    public const int Iterations = 210_000;
    public const int SaltSize = 16;
    public const int HashSize = 32;

    /// <summary>
    /// A hash nobody's password matches. Checked for an email with no account, so that an
    /// unknown email costs the same work, and the same time, as a wrong password (FR-011).
    /// </summary>
    private static readonly string Decoy = Hash("decoy-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, HashSize);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Version}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}");
    }

    public static PasswordVerification Verify(string? stored, string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var parts = (stored ?? "").Split('.');
        if (parts.Length != 4
            || parts[0] != Version
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
            || iterations is < 1 or > 10_000_000)
        {
            return default;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return default;
        }

        if (salt.Length == 0 || expected.Length == 0)
        {
            return default;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
        var matches = CryptographicOperations.FixedTimeEquals(actual, expected);
        return new PasswordVerification(matches, matches && (iterations != Iterations || expected.Length != HashSize));
    }

    /// <summary>Does the work of a check and always answers no.</summary>
    public static PasswordVerification VerifyDecoy(string password)
    {
        Verify(Decoy, password);
        return default;
    }
}

/// <summary>
/// Sign-in codes and the secrets of sessions are never stored, only hashes of them. A code has
/// a million possible values, so its hash is keyed with a server secret: a copy of the database
/// alone is not enough to recover it (research R7).
/// </summary>
public static class CodeHasher
{
    public const int CodeLength = 6;

    public static string NewCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public static byte[] HashCode(byte[] key, Guid challengeId, string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var input = new byte[16 + Encoding.UTF8.GetByteCount(code)];
        challengeId.TryWriteBytes(input);
        Encoding.UTF8.GetBytes(code, input.AsSpan(16));
        return HMACSHA256.HashData(key, input);
    }

    /// <summary>A stand-in for an email that matches no account: comparable, not readable.</summary>
    public static byte[] EmailKey(byte[] key, string normalizedEmail) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("email:" + normalizedEmail));

    public static byte[] NewRenewalSecret() => RandomNumberGenerator.GetBytes(32);

    /// <summary>A renewal secret is 256 random bits, so a plain hash is enough.</summary>
    public static byte[] HashRenewalSecret(byte[] secret) => SHA256.HashData(secret);
}

/// <summary>
/// The rule every password must meet (BRD B-R16): at least ten characters with an upper-case
/// and a lower-case letter, a digit and a symbol. The server is the guard; the screens that
/// show the four lines are spec 004. Shared by the clinic's team and the operators (spec 010).
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

    /// <summary>The rules are met, but the password is the one being replaced (spec 004, FR-026).</summary>
    public const string SameAsCurrent = "SAME_AS_CURRENT";

    public const string Required = "REQUIRED";

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
