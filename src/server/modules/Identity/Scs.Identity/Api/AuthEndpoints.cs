using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tracing;
using Scs.Identity.Contracts;
using Scs.Identity.Domain;
using Scs.Identity.Infrastructure;
using Scs.Identity.Infrastructure.Persistence;
using Scs.SharedKernel;
using Scs.Tenancy.Contracts;

namespace Scs.Identity.Api;

internal sealed record SignInRequest(string? Email, string? Password);

internal sealed record ConfirmCodeRequest(string? Code);

internal sealed record CodeSentResponse(int ExpiresInSeconds, int ResendAvailableInSeconds, int ResendsLeft);

internal sealed record SessionClinic(string NameAr, string NameEn, string State);

/// <param name="Email">The signed-in person's own address, shown back to them. Personal data.</param>
internal sealed record SessionResponse(
    Guid AccountId, [property: PersonalData] string Email, SessionClinic Clinic, DateTimeOffset ExpiresAt)
{
    public override string ToString() => nameof(SessionResponse);
}

/// <summary>
/// The staff sign-in endpoints (contracts/identity-api.openapi.yaml). The three sign-in
/// addresses and renewal handle the session cookies themselves; only <c>GET /auth/session</c>
/// needs a session.
/// </summary>
internal static class AuthEndpoints
{
    public const string SignInLimit = "sign-in";
    public const string RenewalLimit = "session-renewal";

    /// <summary>
    /// Ten sign-in requests a minute per network address, across sign-in, code and resend
    /// (SH-041). Renewal has its own, larger allowance: a clinic's whole team usually shares
    /// one address and each of them renews every few minutes.
    /// </summary>
    public static IServiceCollection AddAuthRateLimits(this IServiceCollection services)
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

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder v1)
    {
        var auth = v1.MapGroup("/auth");

        auth.MapPost("/sign-in", SignInAsync)
            .AllowAnonymous().HandlesOwnSession().RefusesBlockedNetworks().RequireRateLimiting(SignInLimit)
            .WithName("SignIn");

        auth.MapPost("/sign-in/code", ConfirmCodeAsync)
            .AllowAnonymous().HandlesOwnSession().RefusesBlockedNetworks().RequireRateLimiting(SignInLimit)
            .WithName("ConfirmCode");

        auth.MapPost("/sign-in/code/resend", ResendCodeAsync)
            .AllowAnonymous().HandlesOwnSession().RefusesBlockedNetworks().RequireRateLimiting(SignInLimit)
            .WithName("ResendCode");

        auth.MapPost("/session/renew", RenewAsync)
            .AllowAnonymous().HandlesOwnSession().RequireRateLimiting(RenewalLimit)
            .WithName("RenewSession");

        auth.MapPost("/sign-out", SignOutAsync)
            .AllowAnonymous().HandlesOwnSession()
            .WithName("SignOut");

        auth.MapGet("/session", GetSessionAsync).WithName("GetSession");

        return v1;
    }

    private static async Task SignInAsync(
        SignInRequest? request, HttpContext context, SignInService signIn, SessionCookies cookies,
        IdentityDbContext db, IClinicProfile profile, TimeProvider clock, IOptions<ScsOptions> options,
        CancellationToken cancellation)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var (_, emailError) = EmailAddress.TryCreate(request?.Email);
        if (emailError is not null)
        {
            errors["email"] = [emailError];
        }

        if (string.IsNullOrEmpty(request?.Password))
        {
            errors["password"] = ["REQUIRED"];
        }
        else if (request.Password.Length > PasswordRules.MaxLength)
        {
            errors["password"] = [PasswordRules.TooLong];
        }

        if (errors.Count > 0)
        {
            throw ScsProblemException.Validation(errors);
        }

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var outcome = await signIn.SignInAsync(request!.Email, request.Password!, Origin(context), cancellation);

        if (ReferenceEquals(outcome, SignInOutcome.InvalidCredentials))
        {
            // The single answer also takes a single time: whatever was wrong, the caller waits
            // until the same moment after asking (FR-011). It also slows guessing.
            var remaining = SignInService.FailureFloor - System.Diagnostics.Stopwatch.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellation);
            }
        }

        await WriteAsync(outcome, context, cookies, db, profile, clock, options.Value.Auth, cancellation);
    }

    private static async Task ConfirmCodeAsync(
        ConfirmCodeRequest? request, HttpContext context, SignInService signIn, SessionCookies cookies,
        IdentityDbContext db, IClinicProfile profile, TimeProvider clock, IOptions<ScsOptions> options,
        CancellationToken cancellation)
    {
        var ticket = ReadChallenge(context, cookies, clock);
        var outcome = await signIn.ConfirmCodeAsync(ticket, request?.Code, Origin(context), cancellation);
        await WriteAsync(outcome, context, cookies, db, profile, clock, options.Value.Auth, cancellation);
    }

    private static async Task ResendCodeAsync(
        HttpContext context, SignInService signIn, SessionCookies cookies,
        IdentityDbContext db, IClinicProfile profile, TimeProvider clock, IOptions<ScsOptions> options,
        CancellationToken cancellation)
    {
        var ticket = ReadChallenge(context, cookies, clock);
        var outcome = await signIn.ResendAsync(ticket, Origin(context), cancellation);
        await WriteAsync(outcome, context, cookies, db, profile, clock, options.Value.Auth, cancellation);
    }

    private static async Task RenewAsync(
        HttpContext context, SessionService sessions, SessionCookies cookies, TimeProvider clock,
        IOptions<ScsOptions> options, CancellationToken cancellation)
    {
        var ticket = cookies.ReadRenewal(context.Request);
        var renewed = ticket is null ? null : await sessions.RenewAsync(ticket, Origin(context), cancellation);

        if (renewed is null)
        {
            cookies.ClearSession(context.Response);
            await ProblemWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.SessionEnded);
            return;
        }

        WriteSessionCookies(renewed, context, cookies, clock, options.Value.Auth);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    /// <summary>
    /// Ends this browser's session and nothing else. Always succeeds: nobody is left holding a
    /// session they cannot end, whatever state the account or the clinic is in.
    /// </summary>
    private static async Task SignOutAsync(
        HttpContext context, SessionService sessions, SessionCookies cookies, CancellationToken cancellation)
    {
        // Either cookie identifies the session; an access cookie that has run out still does.
        var access = cookies.ReadAccess(context.Request);
        var renewal = access is null ? cookies.ReadRenewal(context.Request) : null;

        if (access is not null)
        {
            await sessions.SignOutAsync(access.SessionId, access.AccountId, access.ClinicId, Origin(context), cancellation);
        }
        else if (renewal is not null)
        {
            await sessions.SignOutAsync(renewal.SessionId, renewal.AccountId, renewal.ClinicId, Origin(context), cancellation);
        }

        cookies.ClearSession(context.Response);
        cookies.ClearChallenge(context.Response);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static async Task<IResult> GetSessionAsync(
        ICurrentStaff staff, IdentityDbContext db, IClinicProfile profile, CancellationToken cancellation)
    {
        var response = await DescribeAsync(staff.AccountId, staff.SessionId, db, profile, cancellation);
        return response is null
            ? throw new ScsProblemException(StatusCodes.Status401Unauthorized, ErrorCodes.SessionEnded)
            : TypedResults.Ok(response);
    }

    // ---------------------------------------------------------------- writing answers

    private static async Task WriteAsync(
        SignInOutcome outcome, HttpContext context, SessionCookies cookies, IdentityDbContext db,
        IClinicProfile profile, TimeProvider clock, AuthOptions auth, CancellationToken cancellation)
    {
        switch (outcome)
        {
            case SignInOutcome.CodeSent sent:
                cookies.WriteChallenge(context.Response, sent.Ticket);
                await context.Response.WriteAsJsonAsync(
                    new CodeSentResponse(sent.ExpiresInSeconds, sent.ResendAvailableInSeconds, sent.ResendsLeft), cancellation);
                break;

            case SignInOutcome.SignedIn signedIn:
                cookies.ClearChallenge(context.Response);
                WriteSessionCookies(signedIn.Session, context, cookies, clock, auth);
                var described = await DescribeAsync(
                    signedIn.Session.AccountId, signedIn.Session.SessionId, db, profile, cancellation);
                await context.Response.WriteAsJsonAsync(described, cancellation);
                break;

            case SignInOutcome.Refused refused:
                if (refused.ClearChallenge)
                {
                    cookies.ClearChallenge(context.Response);
                }

                Dictionary<string, object?>? extra = null;
                if (refused.LockedUntilUtc is { } until)
                {
                    extra = new() { ["lockedUntil"] = until.UtcDateTime.ToString("O") };
                }
                else if (refused.ResendAvailableInSeconds is { } wait)
                {
                    extra = new() { ["resendAvailableInSeconds"] = wait };
                }

                await ProblemWriter.WriteAsync(context, refused.Status, refused.Code, errors: null, extra);
                break;

            default:
                throw new InvalidOperationException("Unknown sign-in outcome.");
        }
    }

    private static void WriteSessionCookies(
        IssuedSession session, HttpContext context, SessionCookies cookies, TimeProvider clock, AuthOptions auth)
    {
        var accessExpires = clock.GetUtcNow().AddMinutes(auth.AccessMinutes);
        if (accessExpires > session.ExpiresAtUtc)
        {
            accessExpires = session.ExpiresAtUtc;
        }

        cookies.WriteAccess(
            context.Response,
            new AccessTicket(session.SessionId, session.AccountId, session.ClinicId, accessExpires),
            session.ExpiresAtUtc);

        if (session.RenewalSecret is { } secret)
        {
            cookies.WriteRenewal(
                context.Response,
                new RenewalTicket(session.SessionId, session.AccountId, session.ClinicId, secret),
                session.ExpiresAtUtc);
        }
    }

    private static async Task<SessionResponse?> DescribeAsync(
        Guid accountId, Guid sessionId, IdentityDbContext db, IClinicProfile profile, CancellationToken cancellation)
    {
        var found = await (
            from session in db.Sessions.AsNoTracking()
            join account in db.Accounts.AsNoTracking() on session.AccountId equals account.Id
            where session.Id == sessionId && account.Id == accountId
            select new { account.Email, session.ExpiresAtUtc }).SingleOrDefaultAsync(cancellation);

        if (found is null)
        {
            return null;
        }

        var clinic = await profile.GetOwnAsync(cancellation);
        return new SessionResponse(
            accountId, found.Email, new SessionClinic(clinic.NameAr, clinic.NameEn, clinic.State), found.ExpiresAtUtc);
    }

    /// <summary>The pending sign-in of this browser, unless its ten minutes are over.</summary>
    private static ChallengeTicket? ReadChallenge(HttpContext context, SessionCookies cookies, TimeProvider clock)
    {
        var ticket = cookies.ReadChallenge(context.Request);
        return ticket is not null && ticket.ExpiresAtUtc > clock.GetUtcNow() ? ticket : null;
    }

    private static RequestOrigin Origin(HttpContext context) => new(
        context.Connection.RemoteIpAddress?.ToString(),
        context.Request.Headers.UserAgent.ToString(),
        TraceIdMiddleware.Get(context));
}
