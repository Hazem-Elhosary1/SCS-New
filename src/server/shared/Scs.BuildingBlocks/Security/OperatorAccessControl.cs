using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Scs.SharedKernel.Access;

namespace Scs.BuildingBlocks.Security;

/// <summary>What an operator may do at this moment: their role and the codes it holds.</summary>
/// <param name="SystemKey">Set for one of the platform's own three roles; null for a custom role.</param>
public sealed record OperatorGrant(Guid? RoleId, string RoleName, string? SystemKey, IReadOnlySet<string> Codes)
{
    public static OperatorGrant Nothing { get; } = new(null, "", null, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// «مسؤول عام» is an identity, not a count of permissions (spec 011, FR-011): a custom role
    /// that holds everything is still not one.
    /// </summary>
    public bool IsSuperAdmin => SystemKey == OperatorPermissions.SystemRoles.SuperAdmin;
}

/// <summary>
/// Reads what an operator may do. Implemented by the Operators module, which owns roles; defined
/// here because the shared project may not reference a module.
/// </summary>
public interface IOperatorAccessSource
{
    Task<OperatorGrant> LoadAsync(Guid operatorId, CancellationToken cancellation);
}

/// <summary>
/// What the operator of this request may do (spec 011, FR-005; contracts/operator-access.md §3).
/// Read once per request, on first use, and never kept between requests: a change to a role is
/// seen by its holders' very next request, and nobody's session ends for it. Ask for a
/// permission by its code; never compare role names.
/// </summary>
public interface IOperatorAccess
{
    ValueTask<OperatorGrant> GetAsync(CancellationToken cancellation);

    ValueTask<bool> HasAsync(string permission, CancellationToken cancellation);

    ValueTask<bool> IsSuperAdminAsync(CancellationToken cancellation);
}

internal sealed class OperatorAccess(IHttpContextAccessor http, IServiceProvider services) : IOperatorAccess
{
    private OperatorGrant? _grant;

    public async ValueTask<OperatorGrant> GetAsync(CancellationToken cancellation)
    {
        if (_grant is not null)
        {
            return _grant;
        }

        var user = http.HttpContext?.User;
        var source = services.GetService<IOperatorAccessSource>();

        if (source is null || user is null || !Guid.TryParse(user.FindFirstValue(OperatorClaims.OperatorId), out var operatorId))
        {
            return _grant = OperatorGrant.Nothing;
        }

        return _grant = await source.LoadAsync(operatorId, cancellation).ConfigureAwait(false);
    }

    public async ValueTask<bool> HasAsync(string permission, CancellationToken cancellation)
    {
        var code = OperatorPermissions.Require(permission).Code;
        return (await GetAsync(cancellation).ConfigureAwait(false)).Codes.Contains(code);
    }

    public async ValueTask<bool> IsSuperAdminAsync(CancellationToken cancellation) =>
        (await GetAsync(cancellation).ConfigureAwait(false)).IsSuperAdmin;
}

public static class OperatorAccessControlExtensions
{
    /// <summary>
    /// Open to operators whose role holds the permission at this moment, and to nobody else
    /// (spec 011, FR-003). Use a constant of <see cref="OperatorPermissions"/>: a code that is
    /// not in the catalogue stops the application from starting.
    /// </summary>
    public static TBuilder RequiresOperator<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        var code = OperatorPermissions.Require(permission).Code;
        builder.ForOperators();
        builder.Add(endpoint => Declare(endpoint, new OperatorRouteMetadata(code)));
        return builder;
    }

    /// <summary>
    /// Open to any signed-in operator, whatever their role. Only for what an operator does to
    /// their own account: the routes that use it are a short written list
    /// (specs/011-operators-roles/contracts/operator-access.md §1), and a test fails when one is
    /// added without changing the list.
    /// </summary>
    public static TBuilder OperatorOwnAccount<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.ForOperators();
        builder.Add(endpoint => Declare(endpoint, OperatorRouteMetadata.OwnAccount));
        return builder;
    }

    private static void Declare(EndpointBuilder endpoint, OperatorRouteMetadata rule)
    {
        if (endpoint.Metadata.OfType<OperatorRouteMetadata>().Any())
        {
            throw new InvalidOperationException(
                $"The route '{endpoint.DisplayName}' declares who may call it more than once.");
        }

        endpoint.Metadata.Add(rule);
    }

    /// <summary>Authenticated by the operators' scheme alone: a clinic's session is none here.</summary>
    private static void ForOperators<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(OperatorSessionAuthenticationHandler.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(OperatorClaims.OperatorId));
}
