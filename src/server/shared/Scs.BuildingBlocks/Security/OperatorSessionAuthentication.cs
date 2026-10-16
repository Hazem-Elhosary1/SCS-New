using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Errors;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Security;

/// <summary>What an operator's session is at this moment; when over, why (spec 010, FR-035).</summary>
/// <param name="EndedFor">Set only when something other than the operator ended it.</param>
public readonly record struct OperatorSessionVerdict(bool Alive, string? EndedFor)
{
    public static OperatorSessionVerdict Yes { get; } = new(true, null);
}

/// <summary>
/// Answers on every request of the operators console: is this session alive right now?
/// Implemented by the Operators module; defined here because the shared project may not
/// reference a module.
/// </summary>
public interface IOperatorSessionCheck
{
    /// <param name="background">
    /// The console asked by itself, not the operator: the session's last activity does not move
    /// (FR-032).
    /// </param>
    Task<OperatorSessionVerdict> CheckAsync(Guid sessionId, Guid operatorId, bool background, CancellationToken cancellation);
}

public static class OperatorClaims
{
    public const string OperatorId = "scs:operator";
    public const string SessionId = "scs:ops-session";
}

/// <summary>The signed-in operator of the request (contracts/operator-sessions.md §1).</summary>
public interface ICurrentOperator
{
    Guid OperatorId { get; }

    Guid SessionId { get; }
}

internal sealed class CurrentOperator(IHttpContextAccessor http) : ICurrentOperator
{
    public Guid OperatorId => Read(OperatorClaims.OperatorId);

    public Guid SessionId => Read(OperatorClaims.SessionId);

    private Guid Read(string claim) =>
        Guid.TryParse(http.HttpContext?.User.FindFirstValue(claim), out var id)
            ? id
            : throw new ScsProblemException(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
}

/// <summary>
/// What a route of the operators console declares about who may call it (spec 011, FR-003):
/// the permission its caller's role must hold, or — for the short list of things an operator
/// does to their own account — none.
/// </summary>
public sealed class OperatorRouteMetadata
{
    internal OperatorRouteMetadata(string? permission) => Permission = permission;

    /// <summary>The code needed; null for a route of the operator's own account.</summary>
    public string? Permission { get; }

    public bool IsOwnAccount => Permission is null;

    public static OperatorRouteMetadata OwnAccount { get; } = new(null);
}

/// <summary>
/// The operator's session (spec 010). It reads the operators' access cookie and nothing else:
/// a clinic's session presented here is no session at all (FR-037). The session's row is
/// checked on every request, so a sign-in elsewhere, a sign-out, a reset or a suspension takes
/// effect on the very next one.
/// </summary>
public sealed class OperatorSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    OperatorCookies cookies,
    TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "OperatorSession";

    /// <summary>The console sets it on what a page asks by itself (contracts/operator-sessions.md §4).</summary>
    public const string BackgroundHeader = "X-Scs-Background";

    private const string RefusalKey = "scs.ops.refusal";
    private const string ReasonKey = "scs.ops.reason";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var ticket = cookies.ReadAccess(Request);
        if (ticket is null)
        {
            return AuthenticateResult.NoResult();
        }

        if (ticket.ExpiresAtUtc <= clock.GetUtcNow())
        {
            Context.Items[RefusalKey] = ErrorCodes.AccessExpired;
            return AuthenticateResult.Fail(ErrorCodes.AccessExpired);
        }

        var check = Context.RequestServices.GetService<IOperatorSessionCheck>();
        var background = Request.Headers[BackgroundHeader].ToString() == "1";
        var verdict = check is null
            ? default
            : await check.CheckAsync(ticket.SessionId, ticket.OperatorId, background, Context.RequestAborted);

        if (!verdict.Alive)
        {
            Context.Items[RefusalKey] = ErrorCodes.SessionEnded;
            Context.Items[ReasonKey] = verdict.EndedFor;
            return AuthenticateResult.Fail(ErrorCodes.SessionEnded);
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(OperatorClaims.OperatorId, ticket.OperatorId.ToString()),
                new Claim(OperatorClaims.SessionId, ticket.SessionId.ToString()),
            ],
            SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>
    /// 401 with the code that tells the console what to do — sign in, renew, or sign in again —
    /// and, once, why a session was ended by something other than the operator.
    /// </summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var code = Context.Items[RefusalKey] as string ?? ErrorCodes.Unauthenticated;
        Dictionary<string, object?>? extra = null;

        if (code == ErrorCodes.SessionEnded)
        {
            // The cookies go with this answer, so the reason is told once.
            cookies.ClearSession(Response);
            if (Context.Items[ReasonKey] is string reason)
            {
                extra = new(StringComparer.Ordinal) { ["reason"] = reason };
            }
        }

        await ProblemWriter.WriteAsync(Context, StatusCodes.Status401Unauthorized, code, errors: null, extra);
    }
}

public static class OperatorSessionExtensions
{
    /// <summary>Adds the operators' scheme beside the clinic's. Neither reads the other's cookies.</summary>
    public static IServiceCollection AddScsOperatorSessions(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<OperatorCookies>();
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentOperator, CurrentOperator>();
        services.TryAddScoped<IOperatorAccess, OperatorAccess>();

        services
            .AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, OperatorSessionAuthenticationHandler>(
                OperatorSessionAuthenticationHandler.SchemeName, _ => { });

        return services;
    }
}
