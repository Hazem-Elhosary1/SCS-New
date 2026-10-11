using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Security;

/// <summary>
/// Says whether sign-in from a network address is blocked. This spec provides the check and
/// the refusal; the list is managed by the operators' security page (spec 025), which replaces
/// the default below. Until then nothing is blocked.
/// </summary>
public interface IBlockedNetworks
{
    ValueTask<bool> IsBlockedAsync(IPAddress? address, CancellationToken cancellation);
}

internal sealed class NoBlockedNetworks : IBlockedNetworks
{
    public ValueTask<bool> IsBlockedAsync(IPAddress? address, CancellationToken cancellation) => ValueTask.FromResult(false);
}

/// <summary>Marks an endpoint that a blocked network may not reach (the sign-in endpoints).</summary>
public sealed class RefusesBlockedNetworksMetadata
{
    public static RefusesBlockedNetworksMetadata Instance { get; } = new();
}

public static class RequestGuardExtensions
{
    /// <summary>Sent by the front ends on every request that changes something.</summary>
    public const string RequestHeader = "X-Scs-Request";

    public static IServiceCollection AddScsRequestGuards(this IServiceCollection services)
    {
        services.TryAddSingleton<IBlockedNetworks, NoBlockedNetworks>();
        return services;
    }

    public static TBuilder RefusesBlockedNetworks<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(RefusesBlockedNetworksMetadata.Instance);
        return builder;
    }

    /// <summary>
    /// Refuses a blocked network before anything else about the request is looked at: before the
    /// request limit is counted and before any account is touched (spec 003, FR-045). Place
    /// before the rate limiter.
    /// </summary>
    public static IApplicationBuilder UseScsBlockedNetworks(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<RefusesBlockedNetworksMetadata>() is not null)
            {
                var blocked = context.RequestServices.GetRequiredService<IBlockedNetworks>();
                if (await blocked.IsBlockedAsync(context.Connection.RemoteIpAddress, context.RequestAborted))
                {
                    await ProblemWriter.WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.NetworkBlocked);
                    return;
                }
            }

            await next(context);
        });

    /// <summary>
    /// A request that changes something on behalf of a session must carry <c>X-Scs-Request: 1</c>
    /// and, when the browser says where it came from, come from one of our own sites (spec 003,
    /// research R10). A form on another site cannot add the header, and the session cookies are
    /// <c>SameSite=Strict</c>; together they stop another site from acting with a signed-in
    /// person's session. An endpoint that is open to everyone and never touches a session
    /// (reporting a front-end failure, a public patient page) has nothing of anyone's to
    /// misuse, and is left alone.
    /// </summary>
    public static IApplicationBuilder UseScsRequestOriginCheck(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var method = context.Request.Method;
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
            {
                await next(context);
                return;
            }

            var endpoint = context.GetEndpoint();
            var usesSession =
                endpoint is not null
                && (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
                    || endpoint.Metadata.GetMetadata<HandlesOwnSessionMetadata>() is not null);

            if (!usesSession)
            {
                await next(context);
                return;
            }

            if (context.Request.Headers[RequestHeader].ToString() != "1" || !OriginAllowed(context))
            {
                await ProblemWriter.WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden);
                return;
            }

            await next(context);
        });

    private static bool OriginAllowed(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length == 0)
        {
            // Not a browser, or a same-origin request from a browser that sends none.
            return true;
        }

        var own = $"{context.Request.Scheme}://{context.Request.Host}";
        if (string.Equals(origin, own, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var allowed = context.RequestServices.GetRequiredService<IOptions<ScsOptions>>().Value.Cors.OriginList;
        return allowed.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }
}
