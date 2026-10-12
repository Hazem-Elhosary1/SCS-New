using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;

namespace Scs.BuildingBlocks.Security;

/// <summary>What an operator's browser holds between the password and the code.</summary>
public sealed record OperatorChallengeTicket(Guid ChallengeId, Guid OperatorId, DateTimeOffset ExpiresAtUtc);

/// <summary>What an operator's browser shows on every request while signed in. Short-lived.</summary>
public sealed record OperatorAccessTicket(Guid SessionId, Guid OperatorId, DateTimeOffset ExpiresAtUtc);

/// <summary>What an operator's browser presents to renew a session. The secret changes at every renewal.</summary>
public sealed record OperatorRenewalTicket(Guid SessionId, Guid OperatorId, byte[] Secret)
{
    public const int SecretLength = 32;

    /// <summary>The type only: this carries a secret.</summary>
    public override string ToString() => nameof(OperatorRenewalTicket);
}

/// <summary>
/// The four cookies of the operators console (spec 010, contracts/operator-sessions.md §2), and
/// the only code that reads or writes them. They are not the clinic's cookies: other names,
/// other purposes on the key ring, so a clinic's session can never be presented as an
/// operator's, nor the reverse (FR-037). All are <c>HttpOnly</c> and <c>Secure</c>.
/// </summary>
public sealed class OperatorCookies(IDataProtectionProvider protection, IOptions<ScsOptions> options)
{
    /// <summary>Between the two steps of sign-in.</summary>
    public const string Challenge = "__Host-scs-ops-signin";

    /// <summary>The session, on every request.</summary>
    public const string Access = "__Host-scs-ops-session";

    /// <summary>Sent only to the operators' sign-in addresses.</summary>
    public const string Renewal = "__Secure-scs-ops-renew";

    public const string RenewalPathSuffix = "/v1/ops/auth";

    /// <summary>The browser's own mark: how a browser an account never used is told from one it has.</summary>
    public const string Device = "__Host-scs-ops-device";

    public const int DeviceMarkLength = 32;

    public static readonly TimeSpan DeviceLife = TimeSpan.FromDays(400);

    private const byte Version = 1;

    private readonly IDataProtector _challenge = protection.CreateProtector("Scs.Ops.Challenge.v1");
    private readonly IDataProtector _access = protection.CreateProtector("Scs.Ops.Access.v1");
    private readonly IDataProtector _renewal = protection.CreateProtector("Scs.Ops.Renewal.v1");
    private readonly IDataProtector _device = protection.CreateProtector("Scs.Ops.Device.v1");

    /// <summary>The path the renewal cookie is limited to.</summary>
    public string RenewalPath => options.Value.Auth.CookiePathBase + RenewalPathSuffix;

    public void WriteChallenge(HttpResponse response, OperatorChallengeTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(ticket);

        response.Cookies.Append(
            Challenge,
            Encode(_challenge, Pack(ticket.ChallengeId, ticket.OperatorId, ticket.ExpiresAtUtc)),
            Options("/", ticket.ExpiresAtUtc));
    }

    public OperatorChallengeTicket? ReadChallenge(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = Decode(_challenge, request.Cookies[Challenge], 41);
        return payload is null
            ? null
            : new OperatorChallengeTicket(new Guid(payload.AsSpan(1, 16)), new Guid(payload.AsSpan(17, 16)), When(payload));
    }

    /// <param name="keepUntil">
    /// When the browser may drop the cookie: the session's end for a remembered session, null for
    /// one that dies with the browser (FR-033).
    /// </param>
    public void WriteAccess(HttpResponse response, OperatorAccessTicket ticket, DateTimeOffset? keepUntil)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(ticket);

        // The cookie outlives its content on purpose: an access cookie that has run out must
        // still arrive, so the platform can say "renew" rather than "sign in".
        response.Cookies.Append(
            Access,
            Encode(_access, Pack(ticket.SessionId, ticket.OperatorId, ticket.ExpiresAtUtc)),
            Options("/", keepUntil));
    }

    /// <summary>The ticket, whether or not it has run out; null when absent or not ours.</summary>
    public OperatorAccessTicket? ReadAccess(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = Decode(_access, request.Cookies[Access], 41);
        return payload is null
            ? null
            : new OperatorAccessTicket(new Guid(payload.AsSpan(1, 16)), new Guid(payload.AsSpan(17, 16)), When(payload));
    }

    public void WriteRenewal(HttpResponse response, OperatorRenewalTicket ticket, DateTimeOffset? keepUntil)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(ticket);

        if (ticket.Secret.Length != OperatorRenewalTicket.SecretLength)
        {
            throw new ArgumentException("A renewal secret is 32 bytes.", nameof(ticket));
        }

        var payload = new byte[33 + OperatorRenewalTicket.SecretLength];
        payload[0] = Version;
        ticket.SessionId.TryWriteBytes(payload.AsSpan(1));
        ticket.OperatorId.TryWriteBytes(payload.AsSpan(17));
        ticket.Secret.CopyTo(payload.AsSpan(33));

        response.Cookies.Append(Renewal, Encode(_renewal, payload), Options(RenewalPath, keepUntil));
        CryptographicOperations.ZeroMemory(payload);
    }

    public OperatorRenewalTicket? ReadRenewal(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = Decode(_renewal, request.Cookies[Renewal], 33 + OperatorRenewalTicket.SecretLength);
        return payload is null
            ? null
            : new OperatorRenewalTicket(
                new Guid(payload.AsSpan(1, 16)),
                new Guid(payload.AsSpan(17, 16)),
                payload.AsSpan(33, OperatorRenewalTicket.SecretLength).ToArray());
    }

    /// <summary>A mark for a browser that has none: thirty-two random bytes, as text.</summary>
    public static string NewDeviceMark() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(DeviceMarkLength));

    /// <summary>The browser's mark; null when it has none or it is not ours.</summary>
    public string? ReadDevice(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = Decode(_device, request.Cookies[Device], 1 + DeviceMarkLength);
        return payload is null ? null : WebEncoders.Base64UrlEncode(payload, 1, DeviceMarkLength);
    }

    public void WriteDevice(HttpResponse response, string mark, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrEmpty(mark);

        var bytes = WebEncoders.Base64UrlDecode(mark);
        if (bytes.Length != DeviceMarkLength)
        {
            throw new ArgumentException("A device mark is 32 bytes.", nameof(mark));
        }

        var payload = new byte[1 + DeviceMarkLength];
        payload[0] = Version;
        bytes.CopyTo(payload.AsSpan(1));

        // Lax, not Strict: the mark must arrive when the operator comes back from a link in an email.
        var cookie = Options("/", now + DeviceLife);
        cookie.SameSite = SameSiteMode.Lax;
        response.Cookies.Append(Device, Encode(_device, payload), cookie);
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

    private static byte[] Pack(Guid first, Guid second, DateTimeOffset when)
    {
        var payload = new byte[41];
        payload[0] = Version;
        first.TryWriteBytes(payload.AsSpan(1));
        second.TryWriteBytes(payload.AsSpan(17));
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(33), when.UtcTicks);
        return payload;
    }

    private static DateTimeOffset When(byte[] payload) =>
        new(BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(33)), TimeSpan.Zero);

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
