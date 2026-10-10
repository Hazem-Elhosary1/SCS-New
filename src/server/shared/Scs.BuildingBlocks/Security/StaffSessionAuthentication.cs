using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Tenancy;
using Scs.SharedKernel;
using Serilog.Context;

namespace Scs.BuildingBlocks.Security;

/// <summary>
/// Answers one question on every request: is this session alive right now? Implemented by the
/// Identity module; defined here because the shared project may not reference a module.
/// </summary>
public interface IStaffSessionCheck
{
    Task<bool> IsAliveAsync(Guid sessionId, Guid accountId, CancellationToken cancellation);
}

/// <summary>
/// Marks an endpoint that handles the session cookies itself (sign-in, renewal, sign-out). The
/// session of a browser that is signing in again must not be established for such a request.
/// </summary>
public sealed class HandlesOwnSessionMetadata
{
    public static HandlesOwnSessionMetadata Instance { get; } = new();
}

public static class StaffClaims
{
    public const string AccountId = "scs:account";
    public const string SessionId = "scs:session";
    public const string ClinicId = "scs:clinic";
}

/// <summary>
/// The staff session (spec 003). Opens the access cookie, establishes the clinic it names, and
/// checks that the session is alive at this moment, so ending a session takes effect on the very
/// next request (CL-SH-124). Everything is refused unless an endpoint says otherwise; this
/// handler only decides who, if anyone, is asking.
/// </summary>
public sealed class StaffSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SessionCookies cookies,
    TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "StaffSession";

    private const string RefusalKey = "scs.auth.refusal";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.GetEndpoint()?.Metadata.GetMetadata<HandlesOwnSessionMetadata>() is not null)
        {
            return AuthenticateResult.NoResult();
        }

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

        var check = Context.RequestServices.GetService<IStaffSessionCheck>();
        var clinic = Context.RequestServices.GetRequiredService<IClinicContext>();
        clinic.Establish(ticket.ClinicId, ClinicSource.Account);

        if (check is null || !await check.IsAliveAsync(ticket.SessionId, ticket.AccountId, Context.RequestAborted))
        {
            Context.Items[RefusalKey] = ErrorCodes.SessionEnded;
            return AuthenticateResult.Fail(ErrorCodes.SessionEnded);
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(StaffClaims.AccountId, ticket.AccountId.ToString()),
                new Claim(StaffClaims.SessionId, ticket.SessionId.ToString()),
                new Claim(StaffClaims.ClinicId, ticket.ClinicId.ToString()),
            ],
            SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>
    /// 401 with the code that tells the client what to do: sign in, renew, or sign in again
    /// because the session was ended.
    /// </summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var code = Context.Items[RefusalKey] as string ?? ErrorCodes.Unauthenticated;

        if (code == ErrorCodes.SessionEnded)
        {
            cookies.ClearSession(Response);
        }

        await ProblemWriter.WriteAsync(Context, StatusCodes.Status401Unauthorized, code);
    }
}

public static class StaffSessionExtensions
{
    /// <summary>
    /// Every route is refused unless it explicitly declares who may call it (constitution
    /// Principle III, SH-056). From spec 003 the refusal is lifted by a live staff session.
    /// </summary>
    public static IServiceCollection AddScsDenyByDefault(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SessionCookies>();

        services
            .AddAuthentication(StaffSessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, StaffSessionAuthenticationHandler>(
                StaffSessionAuthenticationHandler.SchemeName, _ => { });

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    /// <summary>The endpoint reads and writes the session cookies itself.</summary>
    public static TBuilder HandlesOwnSession<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(HandlesOwnSessionMetadata.Instance);
        return builder;
    }

    /// <summary>
    /// Marks every log entry of a signed-in request with who it was done for: identifiers only.
    /// Place after authentication.
    /// </summary>
    public static IApplicationBuilder UseScsStaffLogScope(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var account = context.User.FindFirstValue(StaffClaims.AccountId);
            if (account is null)
            {
                await next(context);
                return;
            }

            using (LogContext.PushProperty("clinicId", context.User.FindFirstValue(StaffClaims.ClinicId)))
            using (LogContext.PushProperty("accountId", account))
            using (LogContext.PushProperty("sessionId", context.User.FindFirstValue(StaffClaims.SessionId)))
            {
                await next(context);
            }
        });
}
