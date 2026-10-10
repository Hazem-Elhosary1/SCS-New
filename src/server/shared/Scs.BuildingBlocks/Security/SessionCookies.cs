using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;

namespace Scs.BuildingBlocks.Security;

/// <summary>What the browser holds between the two steps of sign-in.</summary>
public sealed record ChallengeTicket(Guid ChallengeId, Guid AccountId, Guid ClinicId, DateTimeOffset ExpiresAtUtc);

/// <summary>What the browser shows on every request while signed in. Short-lived.</summary>
public sealed record AccessTicket(Guid SessionId, Guid AccountId, Guid ClinicId, DateTimeOffset ExpiresAtUtc);

/// <summary>What the browser presents to renew a session. The secret changes at every renewal.</summary>
public sealed record RenewalTicket(Guid SessionId, Guid AccountId, Guid ClinicId, byte[] Secret)
{
    public const int SecretLength = 32;

    /// <summary>The type only: this carries a secret.</summary>
    public override string ToString() => nameof(RenewalTicket);
}

/// <summary>
/// The three cookies of staff sign-in (spec 003, research R8 and R9), and the only code that
/// reads or writes them. Each value is encrypted and signed with the platform's key ring under
/// its own purpose, so one kind can never be passed off as another. All three are
/// <c>HttpOnly</c>, <c>Secure</c> and <c>SameSite=Strict</c>: scripts cannot read them and
/// another site cannot make the browser send them.
/// </summary>
public sealed class SessionCookies(IDataProtectionProvider protection, IOptions<ScsOptions> options)
{
    /// <summary>Between the two steps of sign-in.</summary>
    public const string Challenge = "__Host-scs-signin";

    /// <summary>The session, on every request.</summary>
    public const string Access = "__Host-scs-session";

    /// <summary>Sent only to the sign-in addresses.</summary>
    public const string Renewal = "__Secure-scs-renew";

    public const string RenewalPathSuffix = "/v1/auth";

    private const byte Version = 1;

    private readonly IDataProtector _challenge = protection.CreateProtector("Scs.Auth.Challenge.v1");
    private readonly IDataProtector _access = protection.CreateProtector("Scs.Auth.Access.v1");
    private readonly IDataProtector _renewal = protection.CreateProtector("Scs.Auth.Renewal.v1");

    /// <summary>The path the renewal cookie is limited to: the API's base path plus <c>/v1/auth</c>.</summary>
    public string RenewalPath => options.Value.Auth.CookiePathBase + RenewalPathSuffix;

    public void WriteChallenge(HttpResponse response, ChallengeTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(ticket);

        var payload = new byte[1 + 16 + 16 + 16 + 8];
        payload[0] = Version;
        ticket.ChallengeId.TryWriteBytes(payload.AsSpan(1));
        ticket.AccountId.TryWriteBytes(payload.AsSpan(17));
        ticket.ClinicId.TryWriteBytes(payload.AsSpan(33));
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(49), ticket.ExpiresAtUtc.UtcTicks);

        response.Cookies.Append(Challenge, Encode(_challenge, payload), Options("/", ticket.ExpiresAtUtc));
    }

    public ChallengeTicket? ReadChallenge(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = Decode(_challenge, request.Cookies[Challenge], 57);
        return payload is null
            ? null
            : new ChallengeTicket(
                new Guid(payload.AsSpan(1, 16)),
                new Guid(payload.AsSpan(17, 16)),
                new Guid(payload.AsSpan(33, 16)),
                new DateTimeOffset(BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(49)), TimeSpan.Zero));
    }

    public void WriteAccess(HttpResponse response, AccessTicket ticket, DateTimeOffset sessionExpiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(ticket);

        var payload = new byte[1 + 16 + 16 + 16 + 8];
        payload[0] = Version;
        ticket.SessionId.TryWriteBytes(payload.AsSpan(1));
        ticket.AccountId.TryWriteBytes(payload.AsSpan(17));
        ticket.ClinicId.TryWriteBytes(payload.AsSpan(33));
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(49), ticket.ExpiresAtUtc.UtcTicks);

        // The cookie outlives its content on purpose: an access cookie that has run out must
        // still arrive, so the server can say "renew" rather than "sign in".
        response.Cookies.Append(Access, Encode(_access, payload), Options("/", sessionExpiresAtUtc));
    }

    /// <summary>The ticket, whether or not it has run out; null when absent or not ours.</summary>
    public AccessTicket? ReadAccess(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = Decode(_access, request.Cookies[Access], 57);
        return payload is null
            ? null
            : new AccessTicket(
                new Guid(payload.AsSpan(1, 16)),
                new Guid(payload.AsSpan(17, 16)),
                new Guid(payload.AsSpan(33, 16)),
                new DateTimeOffset(BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(49)), TimeSpan.Zero));
    }

    public void WriteRenewal(HttpResponse response, RenewalTicket ticket, DateTimeOffset sessionExpiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(ticket);

        if (ticket.Secret.Length != RenewalTicket.SecretLength)
        {
            throw new ArgumentException("A renewal secret is 32 bytes.", nameof(ticket));
        }

        var payload = new byte[1 + 16 + 16 + 16 + RenewalTicket.SecretLength];
        payload[0] = Version;
        ticket.SessionId.TryWriteBytes(payload.AsSpan(1));
        ticket.AccountId.TryWriteBytes(payload.AsSpan(17));
        ticket.ClinicId.TryWriteBytes(payload.AsSpan(33));
        ticket.Secret.CopyTo(payload.AsSpan(49));

        response.Cookies.Append(Renewal, Encode(_renewal, payload), Options(RenewalPath, sessionExpiresAtUtc));
        CryptographicOperations.ZeroMemory(payload);
    }

    public RenewalTicket? ReadRenewal(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = Decode(_renewal, request.Cookies[Renewal], 49 + RenewalTicket.SecretLength);
        return payload is null
            ? null
            : new RenewalTicket(
                new Guid(payload.AsSpan(1, 16)),
                new Guid(payload.AsSpan(17, 16)),
                new Guid(payload.AsSpan(33, 16)),
                payload.AsSpan(49, RenewalTicket.SecretLength).ToArray());
    }

#pragma warning disable CA1822 // Kept beside the other cookie operations, on the one type that owns them.
    public void ClearChallenge(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(Challenge, Options("/", expires: null));
    }
#pragma warning restore CA1822

    /// <summary>Removes the access and the renewal cookie.</summary>
    public void ClearSession(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(Access, Options("/", expires: null));
        response.Cookies.Delete(Renewal, Options(RenewalPath, expires: null));
    }

    private static CookieOptions Options(string path, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = path,
        Expires = expires,
        IsEssential = true,
    };

    private static string Encode(IDataProtector protector, byte[] payload) =>
        WebEncoders.Base64UrlEncode(protector.Protect(payload));

    private static byte[]? Decode(IDataProtector protector, string? value, int expectedLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 1024)
        {
            return null;
        }

        try
        {
            var payload = protector.Unprotect(WebEncoders.Base64UrlDecode(value));
            return payload.Length == expectedLength && payload[0] == Version ? payload : null;
        }
        catch (Exception refused) when (refused is CryptographicException or FormatException)
        {
            // Not ours, altered, or made with a key that has been retired.
            return null;
        }
    }
}
