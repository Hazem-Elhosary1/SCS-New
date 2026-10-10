using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Scs.BuildingBlocks.Security;

/// <summary>
/// The scheme in force until sign-in exists (spec 003): it authenticates nobody, so the
/// deny-by-default policy answers 401 for every route that does not declare itself anonymous.
/// </summary>
public sealed class NoIdentityAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "NoIdentity";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}

public static class DenyByDefaultExtensions
{
    /// <summary>
    /// Every route is refused unless it explicitly declares who may call it
    /// (constitution Principle III, SH-056).
    /// </summary>
    public static IServiceCollection AddScsDenyByDefault(this IServiceCollection services)
    {
        services
            .AddAuthentication(NoIdentityAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, NoIdentityAuthenticationHandler>(
                NoIdentityAuthenticationHandler.SchemeName, _ => { });

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
