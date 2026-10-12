using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tracing;
using Scs.Operators.Domain;
using Scs.Operators.Infrastructure;
using Scs.SharedKernel;

namespace Scs.Operators.Api;

internal sealed record OpsSignInRequest(string? Email, string? Password, bool Remember);

internal sealed record OpsCodeRequest(string? Code);

internal sealed record OpsForgotRequest(string? Email);

internal sealed record OpsLinkRequest(string? Token);

internal sealed record OpsSetPasswordRequest(string? Token, string? Password);

internal sealed record OpsChangePasswordRequest(string? Current, string? Password);

/// <param name="Email">The address the code went to, shown back to the person who typed it. Personal data.</param>
internal sealed record OpsCodeStep(
    [property: PersonalData] string Email, DateTimeOffset ExpiresAt, DateTimeOffset ResendAt, int ResendsLeft, DateTimeOffset Now)
{
    public override string ToString() => nameof(OpsCodeStep);
}

internal sealed record OpsLinkResponse(string Kind, [property: PersonalData] string Email)
{
    public override string ToString() => nameof(OpsLinkResponse);
}

/// <summary>
/// The addresses of the operators console (contracts/operators-api.openapi.yaml), all under
/// <c>/v1/ops/</c>. Those that sign in handle the operators' cookies themselves and are open to
/// anyone; the rest are for a signed-in operator and nobody else. Every one that changes
/// something refuses a request forged from another site (FR-039a).
/// </summary>
internal static class OperatorEndpoints
{
    public const string SignInLimit = "ops-sign-in";
    public const string RenewalLimit = "ops-session-renewal";

    /// <summary>Why none of these writes to a clinic's audit trail: operators have a record of their own.</summary>
    private const string OwnRecord = "an operator's sign-in events are kept in the operators' own record (spec 010, FR-052), not in a clinic's trail";

    private const string ChallengeForChange = "password-change";

    /// <summary>Ten requests a minute for an address across the addresses before sign-in (SH-041).</summary>
    public static IServiceCollection AddOperatorRateLimits(this IServiceCollection services)
    {
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<ScsOptions>>((limiter, scs) =>
        {
            var auth = scs.Value.Auth;
            limiter.AddPolicy(SignInLimit, context => PerAddress(context, SignInLimit, auth.RequestsPerMinute));
            limiter.AddPolicy(RenewalLimit, context => PerAddress(context, RenewalLimit, auth.RenewalsPerMinute));
        });

        return services;

        static RateLimitPartition<string> PerAddress(HttpContext context, string policy, int perMinute) =>
            RateLimitPartition.GetFixedWindowLimiter(
                $"{policy}:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = perMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
    }

    public static IEndpointRouteBuilder MapOperators(this IEndpointRouteBuilder v1)
    {
        var ops = v1.MapGroup("/ops");
        var auth = ops.MapGroup("/auth");

        auth.MapPost("/sign-in", SignInAsync).Open().WithName("OpsSignIn");
        auth.MapPost("/code", ConfirmCodeAsync).Open().WithName("OpsConfirmCode");
        auth.MapPost("/code/resend", ResendCodeAsync).Open().WithName("OpsResendCode");
        auth.MapPost("/code/cancel", CancelCodeAsync).Open().WithName("OpsCancelCode");

        auth.MapPost("/session/renew", RenewAsync)
            .AllowAnonymous().HandlesOwnSession().RequireRateLimiting(RenewalLimit).RecordsNothing(OwnRecord)
            .WithName("OpsRenewSession");

        auth.MapPost("/sign-out", SignOutAsync)
            .AllowAnonymous().HandlesOwnSession().RecordsNothing(OwnRecord)
            .WithName("OpsSignOut");

        auth.MapGet("/session", GetSessionAsync).OperatorsOnly().WithName("OpsGetSession");

        auth.MapPost("/sign-out-everywhere", SignOutEverywhereAsync)
            .OperatorsOnly().RecordsNothing(OwnRecord)
            .WithName("OpsSignOutEverywhere");

        var password = ops.MapGroup("/password");
        password.MapPost("/forgot", ForgotAsync).Open().WithName("OpsForgotPassword");
        password.MapPost("/link", ReadLinkAsync).Open().WithName("OpsReadPasswordLink");
        password.MapPost("/set", SetPasswordAsync).Open().WithName("OpsSetPassword");

        var profile = ops.MapGroup("/profile");
        profile.MapGet("", GetProfileAsync).OperatorsOnly().WithName("OpsGetProfile");
        profile.MapPost("/password", BeginChangeAsync).OperatorsOnly().RecordsNothing(OwnRecord).WithName("OpsChangePassword");
        profile.MapPost("/password/confirm", ConfirmChangeAsync).OperatorsOnly().RecordsNothing(OwnRecord).WithName("OpsConfirmPasswordChange");
        profile.MapPost("/password/resend", ResendChangeAsync).OperatorsOnly().RecordsNothing(OwnRecord).WithName("OpsResendPasswordChange");
        profile.MapPost("/password/cancel", CancelChangeAsync).OperatorsOnly().RecordsNothing(OwnRecord).WithName("OpsCancelPasswordChange");

        return v1;
    }

    /// <summary>
    /// An address anyone may call before signing in: limited for an address, refused from a
    /// blocked one, and — because it is marked as handling the session itself — refused when
    /// forged from another site.
    /// </summary>
    private static RouteHandlerBuilder Open(this RouteHandlerBuilder builder) =>
        builder.AllowAnonymous().HandlesOwnSession().RefusesBlockedNetworks().RequireRateLimiting(SignInLimit)
            .RecordsNothing(OwnRecord);

    // ---------------------------------------------------------------- signing in

    private static async Task SignInAsync(
        OpsSignInRequest? request, HttpContext context, OperatorSignIn signIn, OperatorCookies cookies,
        OperatorProfile profile, TimeProvider clock, IOptions<ScsOptions> options, CancellationToken cancellation)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(request?.Email))
        {
            errors["email"] = [PasswordRules.Required];
        }
        else if (!OperatorEmail.LooksValid(request.Email))
        {
            errors["email"] = ["INVALID"];
        }

        if (string.IsNullOrEmpty(request?.Password))
        {
            errors["password"] = [PasswordRules.Required];
        }

        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        // A password longer than any we accept is a wrong password, after the same work (spec Edge Cases).
        var password = request!.Password!.Length > PasswordRules.MaxLength ? "" : request.Password;
        var outcome = await signIn.SignInAsync(request.Email, password, request.Remember, Origin(context, cookies), cancellation);

        if (ReferenceEquals(outcome, OpsOutcome.InvalidCredentials))
        {
            // The single answer also takes a single time (FR-011). It also slows guessing.
            var remaining = OperatorSignIn.FailureFloor - System.Diagnostics.Stopwatch.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellation);
            }
        }

        await WriteAsync(outcome, context, cookies, profile, clock, options.Value, cancellation);
    }

    private static async Task ConfirmCodeAsync(
        OpsCodeRequest? request, HttpContext context, OperatorSignIn signIn, OperatorCookies cookies,
        OperatorProfile profile, TimeProvider clock, IOptions<ScsOptions> options, CancellationToken cancellation)
    {
        // A browser with no mark is given one now, so that this sign-in is what makes it known.
        var device = cookies.ReadDevice(context.Request) ?? OperatorCookies.NewDeviceMark();
        var origin = Origin(context, cookies) with { DeviceMark = device };
        var outcome = await signIn.ConfirmCodeAsync(ReadChallenge(context, cookies, clock), request?.Code, origin, cancellation);

        if (outcome is OpsOutcome.SignedIn)
        {
            // A completed sign-in leaves the browser its mark; nothing else does.
            cookies.WriteDevice(context.Response, device, clock.GetUtcNow());
        }

        await WriteAsync(outcome, context, cookies, profile, clock, options.Value, cancellation);
    }

    private static async Task ResendCodeAsync(
        HttpContext context, OperatorCodes codes, OperatorCookies cookies, OperatorProfile profile, TimeProvider clock,
        IOptions<ScsOptions> options, CancellationToken cancellation)
    {
        var outcome = await codes.ResendAsync(
            ReadChallenge(context, cookies, clock), ChallengePurposes.SignIn, Origin(context, cookies), cancellation);
        await WriteAsync(outcome, context, cookies, profile, clock, options.Value, cancellation);
    }

    private static async Task<IResult> CancelCodeAsync(
        HttpContext context, OperatorCodes codes, OperatorCookies cookies, TimeProvider clock, CancellationToken cancellation)
    {
        await codes.CancelAsync(ReadChallenge(context, cookies, clock), ChallengePurposes.SignIn, cancellation);
        cookies.ClearChallenge(context.Response);
        return TypedResults.NoContent();
    }

    // ---------------------------------------------------------------- the session

    private static async Task RenewAsync(
        HttpContext context, OperatorSessions sessions, OperatorCookies cookies, TimeProvider clock,
        IOptions<ScsOptions> options, CancellationToken cancellation)
    {
        var ticket = cookies.ReadRenewal(context.Request);
        var (renewed, endedFor) = ticket is null
            ? (null, null)
            : await sessions.RenewAsync(ticket, Origin(context, cookies), cancellation);

        if (renewed is null)
        {
            cookies.ClearSession(context.Response);
            await ProblemWriter.WriteAsync(
                context, StatusCodes.Status401Unauthorized, ErrorCodes.SessionEnded, errors: null, Reason(endedFor));
            return;
        }

        WriteSessionCookies(renewed, context, cookies, clock, options.Value.Auth);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    /// <summary>Ends this browser's session and nothing else. Always succeeds.</summary>
    private static async Task SignOutAsync(
        HttpContext context, OperatorSessions sessions, OperatorCookies cookies, CancellationToken cancellation)
    {
        // Either cookie identifies the session; an access cookie that has run out still does.
        var access = cookies.ReadAccess(context.Request);
        var renewal = access is null ? cookies.ReadRenewal(context.Request) : null;
        var origin = Origin(context, cookies);

        if (access is not null)
        {
            await sessions.SignOutAsync(access.SessionId, access.OperatorId, origin, cancellation);
        }
        else if (renewal is not null)
        {
            await sessions.SignOutAsync(renewal.SessionId, renewal.OperatorId, origin, cancellation);
        }

        cookies.ClearSession(context.Response);
        cookies.ClearChallenge(context.Response);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static async Task<IResult> GetSessionAsync(
        ICurrentOperator current, OperatorProfile profile, HttpContext context, CancellationToken cancellation)
    {
        var described = await profile.DescribeSessionAsync(current.OperatorId, current.SessionId, Language(context), false, cancellation);
        return described is null
            ? throw new ScsProblemException(StatusCodes.Status401Unauthorized, ErrorCodes.SessionEnded)
            : TypedResults.Ok(described);
    }

    private static async Task<IResult> SignOutEverywhereAsync(
        ICurrentOperator current, HttpContext context, OperatorSessions sessions, OperatorCookies cookies,
        CancellationToken cancellation)
    {
        await sessions.EndAllAsync(
            current.OperatorId, SessionEndings.SignedOutEverywhere, Origin(context, cookies), cancellation);
        cookies.ClearSession(context.Response);
        return TypedResults.NoContent();
    }

    // ---------------------------------------------------------------- links

    private static async Task<IResult> ForgotAsync(
        OpsForgotRequest? request, HttpContext context, OperatorPasswords passwords, OperatorCookies cookies,
        CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(request?.Email) || !OperatorEmail.LooksValid(request.Email))
        {
            throw ScsProblemException.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["email"] = [string.IsNullOrWhiteSpace(request?.Email) ? PasswordRules.Required : "INVALID"],
            });
        }

        // The same answer after the same time, whoever the address belongs to (FR-025).
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        await passwords.ForgotAsync(request.Email, Origin(context, cookies), cancellation);
        var remaining = OperatorSignIn.FailureFloor - System.Diagnostics.Stopwatch.GetElapsedTime(started);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, cancellation);
        }

        return TypedResults.Accepted((string?)null);
    }

    private static async Task<IResult> ReadLinkAsync(
        OpsLinkRequest? request, OperatorPasswords passwords, CancellationToken cancellation)
    {
        var (view, kind) = await passwords.ReadAsync(request?.Token, cancellation);
        return view is null
            ? throw LinkInvalid(kind)
            : TypedResults.Ok(new OpsLinkResponse(view.Kind, view.Email));
    }

    private static async Task SetPasswordAsync(
        OpsSetPasswordRequest? request, HttpContext context, OperatorPasswords passwords, OperatorCookies cookies,
        OperatorProfile profile, TimeProvider clock, IOptions<ScsOptions> options, CancellationToken cancellation)
    {
        var outcome = await passwords.SetAsync(request?.Token, request?.Password, Origin(context, cookies), cancellation);
        await WriteAsync(outcome, context, cookies, profile, clock, options.Value, cancellation);
    }

    // ---------------------------------------------------------------- the profile

    private static async Task<IResult> GetProfileAsync(
        ICurrentOperator current, OperatorProfile profile, HttpContext context, CancellationToken cancellation) =>
        TypedResults.Ok(await profile.ReadAsync(current.OperatorId, current.SessionId, Language(context), cancellation));

    private static async Task BeginChangeAsync(
        OpsChangePasswordRequest? request, ICurrentOperator current, HttpContext context, OperatorPasswords passwords,
        OperatorCookies cookies, OperatorProfile profile, TimeProvider clock, IOptions<ScsOptions> options,
        CancellationToken cancellation)
    {
        var outcome = await passwords.BeginChangeAsync(
            current.OperatorId, current.SessionId, request?.Current, request?.Password, Origin(context, cookies), cancellation);

        if (outcome is OpsOutcome.CodeSent)
        {
            context.Response.StatusCode = StatusCodes.Status202Accepted;
        }

        await WriteAsync(outcome, context, cookies, profile, clock, options.Value, cancellation, ChallengeForChange);
    }

    private static async Task ConfirmChangeAsync(
        OpsCodeRequest? request, ICurrentOperator current, HttpContext context, OperatorPasswords passwords,
        OperatorCookies cookies, OperatorProfile profile, TimeProvider clock, IOptions<ScsOptions> options,
        CancellationToken cancellation)
    {
        var outcome = await passwords.ConfirmChangeAsync(
            ReadChallenge(context, cookies, clock), request?.Code, current.OperatorId, current.SessionId,
            Origin(context, cookies), cancellation);

        if (outcome is OpsOutcome.Done)
        {
            cookies.ClearChallenge(context.Response);
        }

        await WriteAsync(outcome, context, cookies, profile, clock, options.Value, cancellation, ChallengeForChange);
    }

    private static async Task ResendChangeAsync(
        HttpContext context, OperatorCodes codes, OperatorCookies cookies, OperatorProfile profile, TimeProvider clock,
        IOptions<ScsOptions> options, CancellationToken cancellation)
    {
        var outcome = await codes.ResendAsync(
            ReadChallenge(context, cookies, clock), ChallengePurposes.PasswordChange, Origin(context, cookies), cancellation);
        await WriteAsync(outcome, context, cookies, profile, clock, options.Value, cancellation, ChallengeForChange);
    }

    private static async Task<IResult> CancelChangeAsync(
        HttpContext context, OperatorCodes codes, OperatorCookies cookies, TimeProvider clock, CancellationToken cancellation)
    {
        await codes.CancelAsync(ReadChallenge(context, cookies, clock), ChallengePurposes.PasswordChange, cancellation);
        cookies.ClearChallenge(context.Response);
        return TypedResults.NoContent();
    }

    // ---------------------------------------------------------------- writing answers

    private static async Task WriteAsync(
        OpsOutcome outcome, HttpContext context, OperatorCookies cookies, OperatorProfile profile, TimeProvider clock,
        ScsOptions options, CancellationToken cancellation, string purpose = "sign-in")
    {
        switch (outcome)
        {
            case OpsOutcome.CodeSent sent:
                cookies.WriteChallenge(context.Response, sent.Ticket);
                await context.Response.WriteAsJsonAsync(
                    new OpsCodeStep(sent.Email, sent.ExpiresAtUtc, sent.ResendAtUtc, sent.ResendsLeft, sent.Now), cancellation);
                break;

            case OpsOutcome.SignedIn signedIn:
                cookies.ClearChallenge(context.Response);
                WriteSessionCookies(signedIn.Session, context, cookies, clock, options.Auth);
                var described = await profile.DescribeSessionAsync(
                    signedIn.Session.OperatorId, signedIn.Session.SessionId, Language(context), signedIn.Session.NewBrowser,
                    cancellation);
                await context.Response.WriteAsJsonAsync(described, cancellation);
                break;

            case OpsOutcome.Done:
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                break;

            case OpsOutcome.Refused refused:
                if (refused.ClearChallenge)
                {
                    cookies.ClearChallenge(context.Response);
                }

                if (refused.EndedFor is not null)
                {
                    // The step ended the session too (the fifth wrong current password).
                    cookies.ClearSession(context.Response);
                }

                var extra = new Dictionary<string, object?>(StringComparer.Ordinal);
                if (refused.LockedUntilUtc is { } until)
                {
                    extra["lockedUntil"] = until.UtcDateTime.ToString("O");
                    extra["now"] = clock.GetUtcNow().UtcDateTime.ToString("O");
                }

                if (refused.RetryAfterSeconds is { } wait)
                {
                    extra["retryAfterSeconds"] = wait;
                }

                if (refused.AttemptsLeft is { } left)
                {
                    extra["attemptsLeft"] = left;
                }

                if (refused.LinkKind is { } kind)
                {
                    extra["kind"] = kind;
                }

                if (refused.EndedFor is { } reason)
                {
                    extra["reason"] = reason;
                }

                if (refused.Code == ErrorCodes.StepExpired)
                {
                    extra["step"] = purpose;
                }

                await ProblemWriter.WriteAsync(context, refused.Status, refused.Code, errors: null, extra.Count == 0 ? null : extra);
                break;

            default:
                throw new InvalidOperationException("Unknown outcome.");
        }
    }

    /// <summary>
    /// The access cookie lives a few minutes and is renewed in use. A session that is not
    /// remembered leaves cookies that die with the browser (FR-033).
    /// </summary>
    private static void WriteSessionCookies(
        IssuedOperatorSession session, HttpContext context, OperatorCookies cookies, TimeProvider clock, AuthOptions auth)
    {
        var accessExpires = clock.GetUtcNow().AddMinutes(auth.AccessMinutes);
        if (accessExpires > session.ExpiresAtUtc)
        {
            accessExpires = session.ExpiresAtUtc;
        }

        DateTimeOffset? keepUntil = session.Remember ? session.ExpiresAtUtc : null;
        cookies.WriteAccess(
            context.Response, new OperatorAccessTicket(session.SessionId, session.OperatorId, accessExpires), keepUntil);

        if (session.RenewalSecret is { } secret)
        {
            cookies.WriteRenewal(
                context.Response, new OperatorRenewalTicket(session.SessionId, session.OperatorId, secret), keepUntil);
        }
    }

    private static Dictionary<string, object?>? Reason(string? endedFor) =>
        endedFor is null ? null : new(StringComparer.Ordinal) { ["reason"] = endedFor };

    private static ScsProblemException LinkInvalid(string? kind) =>
        new(StatusCodes.Status410Gone, ErrorCodes.LinkInvalid)
        {
            Extensions = kind is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = kind },
        };

    /// <summary>The pending step of this browser, unless its time is over.</summary>
    private static OperatorChallengeTicket? ReadChallenge(HttpContext context, OperatorCookies cookies, TimeProvider clock)
    {
        var ticket = cookies.ReadChallenge(context.Request);
        return ticket is not null && ticket.ExpiresAtUtc > clock.GetUtcNow() ? ticket : null;
    }

    private static string Language(HttpContext context) =>
        context.Request.Headers.AcceptLanguage.ToString().StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";

    private static OpsOrigin Origin(HttpContext context, OperatorCookies cookies)
    {
        _ = cookies;
        return new OpsOrigin(
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            TraceIdMiddleware.Get(context));
    }
}
