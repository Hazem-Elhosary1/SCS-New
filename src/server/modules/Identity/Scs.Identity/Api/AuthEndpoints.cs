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
using Scs.SharedKernel.Access;
using Scs.Subscriptions.Contracts;
using Scs.Tenancy.Contracts;

namespace Scs.Identity.Api;

internal sealed record SignInRequest(string? Email, string? Password);

internal sealed record ConfirmCodeRequest(string? Code);

internal sealed record CodeSentResponse(int ExpiresInSeconds, int ResendAvailableInSeconds, int ResendsLeft);

internal sealed record SessionClinic(string NameAr, string NameEn, string State);

/// <summary>A feature's name in both languages.</summary>
internal sealed record SessionName(string Ar, string En);

/// <summary>
/// The clinic's plan as the console needs it to say what is not in it (spec 014, FR-025): its name,
/// the built features it does not include, and those features' names. Nothing about other plans.
/// </summary>
/// <param name="NameAr">Null when the clinic has no plan.</param>
internal sealed record SessionPlan(string? NameAr, string? NameEn, IReadOnlyList<string> Withheld, IReadOnlyDictionary<string, SessionName> Names);

/// <param name="ReadyKey">Set for a ready role that still has the name it was created with.</param>
internal sealed record SessionRole(Guid Id, string Name, string? ReadyKey);

/// <param name="Email">The signed-in person's own address, shown back to them. Personal data.</param>
/// <param name="MustChangePassword">The account holds a temporary password: only replacing it is allowed (spec 004).</param>
/// <param name="Role">The role the account holds; null when it holds none, and then it holds no permission.</param>
/// <param name="Permissions">
/// The codes the person holds at this moment, in catalogue order (spec 005, FR-027). A screen
/// uses them to leave out what the person may not use; the server decides again on every request.
/// </param>
/// <param name="Preferences">What the person keeps between sessions (spec 006).</param>
/// <param name="Plan">What the clinic's plan leaves out, for the console's not-in-plan state (spec 014).</param>
internal sealed record SessionResponse(
    Guid AccountId, [property: PersonalData] string Email, SessionClinic Clinic, DateTimeOffset ExpiresAt,
    bool MustChangePassword, SessionRole? Role, IReadOnlyList<string> Permissions, PreferencesView Preferences, SessionPlan Plan)
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
            .RecordedBySignIn()
            .WithName("SignIn");

        auth.MapPost("/sign-in/code", ConfirmCodeAsync)
            .AllowAnonymous().HandlesOwnSession().RefusesBlockedNetworks().RequireRateLimiting(SignInLimit)
            .RecordedBySignIn()
            .WithName("ConfirmCode");

        auth.MapPost("/sign-in/code/resend", ResendCodeAsync)
            .AllowAnonymous().HandlesOwnSession().RefusesBlockedNetworks().RequireRateLimiting(SignInLimit)
            .RecordsNothing("sends a sign-in code again; nothing changes until it is used")
            .WithName("ResendCode");

        auth.MapPost("/session/renew", RenewAsync)
            .AllowAnonymous().HandlesOwnSession().RequireRateLimiting(RenewalLimit)
            .RecordedBySignIn()
            .WithName("RenewSession");

        auth.MapPost("/sign-out", SignOutAsync)
            .AllowAnonymous().HandlesOwnSession()
            .RecordedBySignIn()
            .WithName("SignOut");

        auth.MapGet("/session", GetSessionAsync).OwnAccountOnly().AllowsTemporaryPassword().WithName("GetSession");

        return v1;
    }

    private static async Task SignInAsync(
        SignInRequest? request, HttpContext context, SignInService signIn, SessionCookies cookies,
        IdentityDbContext db, IClinicProfile profile, StaffAccessSource access, Preferences preferences, TimeProvider clock, IOptions<ScsOptions> options,
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
        var device = cookies.ReadDevice(context.Request) ?? SessionCookies.NewDeviceMark();
        var outcome = await signIn.SignInAsync(
            request!.Email, request.Password!, Origin(context) with { DeviceMark = device }, cancellation);
        KeepDeviceMark(outcome, device, context, cookies, clock);

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

        await WriteAsync(outcome, context, cookies, db, profile, access, preferences, clock, options.Value.Auth, cancellation);
    }

    private static async Task ConfirmCodeAsync(
        ConfirmCodeRequest? request, HttpContext context, SignInService signIn, SessionCookies cookies,
        IdentityDbContext db, IClinicProfile profile, StaffAccessSource access, Preferences preferences, TimeProvider clock, IOptions<ScsOptions> options,
        CancellationToken cancellation)
    {
        var ticket = ReadChallenge(context, cookies, clock);
        var device = cookies.ReadDevice(context.Request) ?? SessionCookies.NewDeviceMark();
        var outcome = await signIn.ConfirmCodeAsync(
            ticket, request?.Code, Origin(context) with { DeviceMark = device }, cancellation);
        KeepDeviceMark(outcome, device, context, cookies, clock);
        await WriteAsync(outcome, context, cookies, db, profile, access, preferences, clock, options.Value.Auth, cancellation);
    }

    /// <summary>
    /// A completed sign-in leaves the browser its mark, for thirteen months from now. Nothing
    /// else does: a failed or refused sign-in marks no browser (spec 008, FR-043).
    /// </summary>
    private static void KeepDeviceMark(
        SignInOutcome outcome, string device, HttpContext context, SessionCookies cookies, TimeProvider clock)
    {
        if (outcome is SignInOutcome.SignedIn)
        {
            cookies.WriteDevice(context.Response, device, clock.GetUtcNow());
        }
    }

    private static async Task ResendCodeAsync(
        HttpContext context, SignInService signIn, SessionCookies cookies,
        IdentityDbContext db, IClinicProfile profile, StaffAccessSource access, Preferences preferences, TimeProvider clock, IOptions<ScsOptions> options,
        CancellationToken cancellation)
    {
        var ticket = ReadChallenge(context, cookies, clock);
        var outcome = await signIn.ResendAsync(ticket, Origin(context), cancellation);
        await WriteAsync(outcome, context, cookies, db, profile, access, preferences, clock, options.Value.Auth, cancellation);
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
        ICurrentStaff staff, IdentityDbContext db, IClinicProfile profile, StaffAccessSource access,
        Preferences preferences, IClinicEntitlements entitlements, CancellationToken cancellation)
    {
        var response = await DescribeAsync(staff.AccountId, staff.SessionId, db, profile, access, preferences, entitlements, cancellation);
        return response is null
            ? throw new ScsProblemException(StatusCodes.Status401Unauthorized, ErrorCodes.SessionEnded)
            : TypedResults.Ok(response);
    }

    // ---------------------------------------------------------------- writing answers

    private static async Task WriteAsync(
        SignInOutcome outcome, HttpContext context, SessionCookies cookies, IdentityDbContext db,
        IClinicProfile profile, StaffAccessSource access, Preferences preferences, TimeProvider clock, AuthOptions auth,
        CancellationToken cancellation)
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
                    signedIn.Session.AccountId, signedIn.Session.SessionId, db, profile, access, preferences,
                    context.RequestServices.GetRequiredService<IClinicEntitlements>(), cancellation);
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
        Guid accountId, Guid sessionId, IdentityDbContext db, IClinicProfile profile, StaffAccessSource access,
        Preferences preferences, IClinicEntitlements entitlements, CancellationToken cancellation)
    {
        var found = await (
            from session in db.Sessions.AsNoTracking()
            join account in db.Accounts.AsNoTracking() on session.AccountId equals account.Id
            where session.Id == sessionId && account.Id == accountId
            select new { account.Email, session.ExpiresAtUtc, account.MustChangePassword }).SingleOrDefaultAsync(cancellation);

        if (found is null)
        {
            return null;
        }

        var clinic = await profile.GetOwnAsync(cancellation);
        var grant = await access.LoadAsync(accountId, cancellation);
        var plan = await entitlements.CurrentAsync(cancellation);
        var withheld = plan.Withheld;

        // A temporary password opens nothing but its own replacement, so nothing is listed.
        return new SessionResponse(
            accountId, found.Email, new SessionClinic(clinic.NameAr, clinic.NameEn, clinic.State), found.ExpiresAtUtc,
            found.MustChangePassword,
            grant.RoleId is { } roleId ? new SessionRole(roleId, grant.RoleName!, grant.ReadyKey) : null,
            found.MustChangePassword ? [] : PermissionCatalogue.InOrder(grant.Codes),
            await preferences.ReadAsync(accountId, cancellation),
            new SessionPlan(
                plan.PlanNameAr,
                plan.PlanNameEn,
                withheld,
                withheld.ToDictionary(key => key, key => new SessionName(plan.Of(key).NameAr, plan.Of(key).NameEn), StringComparer.Ordinal)));
    }

    /// <summary>The pending sign-in of this browser, unless its ten minutes are over.</summary>
    private static ChallengeTicket? ReadChallenge(HttpContext context, SessionCookies cookies, TimeProvider clock)
    {
        var ticket = cookies.ReadChallenge(context.Request);
        return ticket is not null && ticket.ExpiresAtUtc > clock.GetUtcNow() ? ticket : null;
    }

    internal static RequestOrigin Origin(HttpContext context) => new(
        context.Connection.RemoteIpAddress?.ToString(),
        context.Request.Headers.UserAgent.ToString(),
        TraceIdMiddleware.Get(context));
}
