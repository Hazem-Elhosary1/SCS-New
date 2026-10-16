using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scs.BuildingBlocks.Errors;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;

namespace Scs.BuildingBlocks.Security;

/// <summary>Who may call a route, beyond "everyone" (which stays <c>AllowAnonymous()</c>).</summary>
public enum AccessRuleKind
{
    /// <summary>Any signed-in person: only for what a person does to their own account.</summary>
    OwnAccount = 0,

    /// <summary>Holders of one permission of the catalogue.</summary>
    Permission = 1,

    /// <summary>Holders of the permission, or the owner of the record the request names (SH-055).</summary>
    PermissionOrOwner = 2,
}

/// <summary>
/// What a route declares about who may call it (spec 005, FR-019). A route that carries neither
/// this nor <c>AllowAnonymous()</c> is refused for everyone.
/// </summary>
public sealed class AccessRuleMetadata
{
    private AccessRuleMetadata(AccessRuleKind kind, string? permission, Type? ownerCheck)
    {
        Kind = kind;
        Permission = permission;
        OwnerCheck = ownerCheck;
    }

    public AccessRuleKind Kind { get; }

    /// <summary>The code needed; null for <see cref="AccessRuleKind.OwnAccount"/>.</summary>
    public string? Permission { get; }

    /// <summary>The <see cref="IRecordOwnerCheck"/> to ask; only for <see cref="AccessRuleKind.PermissionOrOwner"/>.</summary>
    public Type? OwnerCheck { get; }

    public static AccessRuleMetadata OwnAccount { get; } = new(AccessRuleKind.OwnAccount, null, null);

    public static AccessRuleMetadata For(string permission) =>
        new(AccessRuleKind.Permission, PermissionCatalogue.Require(permission).Code, null);

    public static AccessRuleMetadata For(string permission, Type ownerCheck) =>
        new(AccessRuleKind.PermissionOrOwner, PermissionCatalogue.Require(permission).Code, ownerCheck);
}

/// <summary>
/// Says whether the person asking owns the record this request names. What "owner" means is the
/// module's to say (spec 005, FR-026). Asked only when the person lacks the permission, and
/// "no such record" is answered <c>false</c>: the refusal is then the same as for anyone else.
/// </summary>
public interface IRecordOwnerCheck
{
    ValueTask<bool> IsOwnerAsync(HttpContext context, CancellationToken cancellation);
}

/// <summary>How wide a person may see through a view permission.</summary>
public enum StaffReach
{
    /// <summary>They do not hold the view.</summary>
    None = 0,

    /// <summary>Their own records only.</summary>
    Own = 1,

    /// <summary>Everyone's.</summary>
    All = 2,
}

/// <summary>What an account may do at this moment: its role's name and the codes that role holds.</summary>
/// <param name="RoleId">Null when the account holds no role; it then holds no permission.</param>
/// <param name="ReadyKey">Set for a ready role that still has the name it was created with.</param>
public sealed record StaffGrant(Guid? RoleId, string? RoleName, string? ReadyKey, IReadOnlySet<string> Codes)
{
    public static StaffGrant Nothing { get; } = new(null, null, null, new HashSet<string>(StringComparer.Ordinal));
}

/// <summary>
/// Reads what an account may do. Implemented by the Identity module, which owns roles; defined
/// here because the shared project may not reference a module.
/// </summary>
public interface IStaffAccessSource
{
    Task<StaffGrant> LoadAsync(Guid accountId, CancellationToken cancellation);
}

/// <summary>
/// What the person of this request may do (spec 005, FR-025). Read once per request, on first
/// use, and never kept between requests: a change to a role is seen by the very next request
/// (FR-024). Ask for a permission by its code; never compare role names.
/// </summary>
public interface IStaffAccess
{
    ValueTask<StaffGrant> GetAsync(CancellationToken cancellation);

    ValueTask<bool> HasAsync(string permission, CancellationToken cancellation);

    /// <summary>
    /// For a view permission: nothing, their own only, or everyone's — the last when they hold
    /// the scope permission that widens it, or when the view has none.
    /// </summary>
    ValueTask<StaffReach> ReachOfAsync(string viewPermission, CancellationToken cancellation);
}

internal sealed class StaffAccess(IHttpContextAccessor http, IServiceProvider services) : IStaffAccess
{
    private StaffGrant? _grant;

    public async ValueTask<StaffGrant> GetAsync(CancellationToken cancellation)
    {
        if (_grant is not null)
        {
            return _grant;
        }

        var user = http.HttpContext?.User;
        var source = services.GetService<IStaffAccessSource>();

        // A temporary password opens nothing but its own replacement (spec 004).
        if (source is null
            || user is null
            || user.HasClaim(claim => claim.Type == StaffClaims.MustChangePassword)
            || !Guid.TryParse(user.FindFirstValue(StaffClaims.AccountId), out var accountId))
        {
            return _grant = StaffGrant.Nothing;
        }

        return _grant = await source.LoadAsync(accountId, cancellation).ConfigureAwait(false);
    }

    public async ValueTask<bool> HasAsync(string permission, CancellationToken cancellation)
    {
        var code = PermissionCatalogue.Require(permission).Code;
        return (await GetAsync(cancellation).ConfigureAwait(false)).Codes.Contains(code);
    }

    public async ValueTask<StaffReach> ReachOfAsync(string viewPermission, CancellationToken cancellation)
    {
        var view = PermissionCatalogue.Require(viewPermission);
        var codes = (await GetAsync(cancellation).ConfigureAwait(false)).Codes;

        if (!codes.Contains(view.Code))
        {
            return StaffReach.None;
        }

        var scope = PermissionCatalogue.ScopeOf(view.Code);
        return scope is null || codes.Contains(scope.Code) ? StaffReach.All : StaffReach.Own;
    }
}

public static partial class AccessControlExtensions
{
    /// <summary>
    /// Open to any signed-in person. Only for what a person does to their own account: the
    /// routes that use it are a short written list (specs/005-permissions-engine/contracts/
    /// access-engine.md §1), and a test fails when one is added without changing the list.
    /// </summary>
    public static TBuilder OwnAccountOnly<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.Declare(AccessRuleMetadata.OwnAccount);

    /// <summary>Open to holders of one permission. Use a constant of <see cref="Permissions"/>.</summary>
    public static TBuilder Requires<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.Declare(AccessRuleMetadata.For(permission));

    /// <summary>Open to holders of the permission, and to the owner of the record the request names.</summary>
    public static RouteHandlerBuilder RequiresOrOwner<TCheck>(this RouteHandlerBuilder builder, string permission)
        where TCheck : IRecordOwnerCheck =>
        builder.Declare(AccessRuleMetadata.For(permission, typeof(TCheck)));

    private static TBuilder Declare<TBuilder>(this TBuilder builder, AccessRuleMetadata rule)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpoint =>
        {
            if (endpoint.Metadata.OfType<AccessRuleMetadata>().Any())
            {
                throw new InvalidOperationException(
                    $"The route '{endpoint.DisplayName}' declares who may call it more than once.");
            }

            endpoint.Metadata.Add(rule);
        });
        return builder;
    }

    /// <summary>
    /// Decides whether the signed-in person may call the route, before its handler runs
    /// (constitution Principle III, SH-056). A route that declares nothing is refused for
    /// everyone; a missing permission is refused naming it, and nothing has been read or changed
    /// by then, so the answer cannot depend on whether a record exists (spec 005, FR-022).
    /// Place after authorization: a request with no session has already been answered 401.
    /// </summary>
    public static IApplicationBuilder UseScsAccessGate(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            if (endpoint is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                await next(context);
                return;
            }

            // A route of the operators console: its own scheme has already let in a signed-in
            // operator and nobody else (spec 010); what that operator's role holds decides the
            // rest (spec 011). An operator's permission never opens a clinic's route, nor the
            // reverse: the two rules are read from different declarations.
            if (endpoint.Metadata.GetMetadata<OperatorRouteMetadata>() is { } operatorRule)
            {
                if (operatorRule.IsOwnAccount
                    || await context.RequestServices.GetRequiredService<IOperatorAccess>()
                        .HasAsync(operatorRule.Permission!, context.RequestAborted))
                {
                    await next(context);
                    return;
                }

                var opsLogger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Scs.Access");
                var opsRoute = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? endpoint.DisplayName ?? "";
                LogRefused(opsLogger, opsRoute, operatorRule.Permission!);
                await ProblemWriter.WriteAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    ErrorCodes.PermissionRequired,
                    extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["permission"] = operatorRule.Permission });
                return;
            }

            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Scs.Access");
            var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? endpoint.DisplayName ?? "";
            var rule = endpoint.Metadata.GetMetadata<AccessRuleMetadata>();

            if (rule is null)
            {
                LogUndeclared(logger, route);
                await ProblemWriter.WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden);
                return;
            }

            if (rule.Kind == AccessRuleKind.OwnAccount)
            {
                await next(context);
                return;
            }

            var access = context.RequestServices.GetRequiredService<IStaffAccess>();
            var allowed = await access.HasAsync(rule.Permission!, context.RequestAborted);

            if (!allowed && rule.OwnerCheck is { } checkType)
            {
                var check = (IRecordOwnerCheck)ActivatorUtilities.GetServiceOrCreateInstance(context.RequestServices, checkType);
                allowed = await check.IsOwnerAsync(context, context.RequestAborted);
            }

            if (allowed)
            {
                await next(context);
                return;
            }

            LogRefused(logger, route, rule.Permission!);
            await ProblemWriter.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                ErrorCodes.PermissionRequired,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["permission"] = rule.Permission });
        });

    // The account and the clinic are on every entry of a signed-in request already (the staff
    // log scope); these add the route and the code, and nothing personal.
    [LoggerMessage(Level = LogLevel.Information, Message = "Refused {route}: the permission {permission} is not held")]
    private static partial void LogRefused(ILogger logger, string route, string permission);

    [LoggerMessage(Level = LogLevel.Error, Message = "Refused {route}: it does not declare who may call it")]
    private static partial void LogUndeclared(ILogger logger, string route);
}
