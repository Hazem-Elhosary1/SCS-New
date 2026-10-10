using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Scs.BuildingBlocks.Security;

/// <summary>What an emailed password link carries, once opened.</summary>
/// <param name="Kind"><c>reset</c> or <c>new-account</c>.</param>
/// <param name="ClinicId">The clinic of the link's account: what lets the link be read under isolation.</param>
/// <param name="Secret">Random; only its hash is stored with the link.</param>
#pragma warning disable CA1819 // A secret is a byte array.
public sealed record PasswordLinkToken(string Kind, Guid ClinicId, Guid LinkId, byte[] Secret)
#pragma warning restore CA1819
{
    /// <summary>The type only: this carries a secret.</summary>
    public override string ToString() => nameof(PasswordLinkToken);
}

/// <summary>
/// The token inside an emailed password link (spec 004, research R1 and R2). It is encrypted and
/// signed with the platform's key ring under its own purpose, so it cannot be forged and a
/// session cookie can never be passed off as a link. Before sign-in there is no clinic; the
/// token names its own, which is how the link is found without stepping around isolation. The
/// token alone opens nothing: the link's row holds a hash of the secret inside it.
/// Beside <see cref="SessionCookies"/>, this is the only code that creates a protector.
/// </summary>
public sealed class PasswordLinkTokens(IDataProtectionProvider protection)
{
    public const string Reset = "reset";
    public const string NewAccount = "new-account";

    public const int SecretLength = 32;

    /// <summary>Longer than any token we issue; anything above it is refused unread.</summary>
    public const int MaxTokenLength = 1024;

    private const byte Version = 1;
    private const int PayloadLength = 1 + 1 + 16 + 16 + SecretLength;

    private readonly IDataProtector _protector = protection.CreateProtector("Scs.PasswordLink.v1");

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretLength);

    /// <summary>A link's secret is 256 random bits, so a plain hash is enough.</summary>
    public static byte[] Hash(byte[] secret) => SHA256.HashData(secret);

    public static bool Matches(byte[] secret, byte[] storedHash) =>
        CryptographicOperations.FixedTimeEquals(Hash(secret), storedHash);

    public string Write(PasswordLinkToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (token.Secret.Length != SecretLength)
        {
            throw new ArgumentException("A link secret is 32 bytes.", nameof(token));
        }

        var payload = new byte[PayloadLength];
        payload[0] = Version;
        payload[1] = token.Kind switch
        {
            Reset => 1,
            NewAccount => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(token)),
        };
        token.ClinicId.TryWriteBytes(payload.AsSpan(2));
        token.LinkId.TryWriteBytes(payload.AsSpan(18));
        token.Secret.CopyTo(payload.AsSpan(34));

        return WebEncoders.Base64UrlEncode(_protector.Protect(payload));
    }

    /// <summary>Null for anything that is not a token we made: altered, cut short, foreign, or too long.</summary>
    public PasswordLinkToken? Read(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            return null;
        }

        try
        {
            var payload = _protector.Unprotect(WebEncoders.Base64UrlDecode(token));
            if (payload.Length != PayloadLength || payload[0] != Version)
            {
                return null;
            }

            var kind = payload[1] switch
            {
                1 => Reset,
                2 => NewAccount,
                _ => null,
            };

            return kind is null
                ? null
                : new PasswordLinkToken(
                    kind,
                    new Guid(payload.AsSpan(2, 16)),
                    new Guid(payload.AsSpan(18, 16)),
                    payload.AsSpan(34, SecretLength).ToArray());
        }
        catch (Exception refused) when (refused is CryptographicException or FormatException)
        {
            // Not ours, altered, or made with a key that has been retired.
            return null;
        }
    }
}
