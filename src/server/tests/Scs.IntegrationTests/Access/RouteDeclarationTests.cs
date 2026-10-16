using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Security;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Identity;
using Scs.SharedKernel.Access;

namespace Scs.IntegrationTests.Access;

/// <summary>
/// Spec 005, User Story 2 (SH-056, ق-38, FR-019 to FR-021): no route ships without saying who
/// may call it. This is the check that fails the build for one that says nothing.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RouteDeclarationTests(SqlServerFixture sql)
{
    /// <summary>
    /// The whole list of routes open to any signed-in person: what a person does to their own
    /// account. Adding to it means changing this list and contracts/access-engine.md §1, in review.
    /// </summary>
    private static readonly string[] OwnAccountRoutes =
    [
        "PUT /v1/account/preferences", "POST /v1/auth/password/change", "GET /v1/auth/session",
        "GET /v1/notifications", "GET /v1/notifications/state", "POST /v1/notifications/{id:guid}/read",
        "POST /v1/notifications/read-all",

        // Spec 009: decided by the kind's owner, the uploader's identity and the token.
        "POST /v1/files", "DELETE /v1/files/{id:guid}", "POST /v1/files/{id:guid}/access", "GET /v1/files/content/{token}",

        // The builders' kind, on a developer's machine only.
        "GET /v1/dev/files/sample", "POST /v1/dev/files/sample",
        "POST /v1/dev/files/sample/{id:guid}/remove", "POST /v1/dev/files/sample/{id:guid}/restore",
    ];

    /// <summary>
    /// The same for the operators console (spec 011, contracts/operator-access.md §1): only what
    /// an operator does to their own account. Everything else names a permission.
    /// </summary>
    private static readonly string[] OperatorOwnAccountRoutes =
    [
        "GET /v1/ops/auth/session", "POST /v1/ops/auth/sign-out-everywhere",
        "GET /v1/ops/profile/", "POST /v1/ops/profile/password", "POST /v1/ops/profile/password/confirm",
        "POST /v1/ops/profile/password/resend", "POST /v1/ops/profile/password/cancel",
    ];

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        return $"{(methods is { Count: > 0 } ? methods[0] : "ANY")} {endpoint.RoutePattern.RawText}";
    }

    private static List<RouteEndpoint> Routes(ApiFactory factory, bool probes) =>
    [
        .. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? "").StartsWith("/probe", StringComparison.Ordinal) == probes),
    ];

    [Fact]
    public async Task Every_route_of_the_platform_declares_exactly_one_thing_about_who_may_call_it()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var routes = Routes(factory, probes: false);

        Assert.NotEmpty(routes);

        var offenders = routes
            .Where(e =>
                (e.Metadata.GetMetadata<IAllowAnonymous>() is null ? 0 : 1)
                + e.Metadata.OfType<AccessRuleMetadata>().Count()
                + e.Metadata.OfType<OperatorRouteMetadata>().Count() != 1)
            .Select(Describe)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Say who may call each of these — AllowAnonymous(), OwnAccountOnly(), Requires(...), RequiresOrOwner(...) or, in the operators console, OperatorOwnAccount() or RequiresOperator(...), and only one: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// Spec 010 (FR-037, FR-039): the operators console is its own room. Every route under
    /// <c>/v1/ops/</c> is open to anyone or to a signed-in operator, and never to a clinic's
    /// session; and no route outside it is declared for operators.
    /// </summary>
    [Fact]
    public async Task The_routes_of_the_operators_console_are_for_operators_and_no_others_are()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();
        var routes = Routes(factory, probes: false);

        var ops = routes.Where(e => e.RoutePattern.RawText!.StartsWith("/v1/ops/", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(ops);
        Assert.All(ops, e => Assert.Empty(e.Metadata.OfType<AccessRuleMetadata>()));
        Assert.All(
            ops.Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null),
            e => Assert.Contains(
                OperatorSessionAuthenticationHandler.SchemeName,
                e.Metadata.OfType<Microsoft.AspNetCore.Authorization.AuthorizationPolicy>().SelectMany(p => p.AuthenticationSchemes)));

        Assert.DoesNotContain(
            routes.Except(ops), e => e.Metadata.GetMetadata<OperatorRouteMetadata>() is not null);
    }

    [Fact]
    public async Task The_routes_open_to_any_signed_in_operator_are_exactly_the_written_list()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        var found = Routes(factory, probes: false)
            .Where(e => e.Metadata.GetMetadata<OperatorRouteMetadata>() is { IsOwnAccount: true })
            .Select(Describe)
            .Order(StringComparer.Ordinal);

        Assert.Equal(OperatorOwnAccountRoutes.Order(StringComparer.Ordinal), found);
    }

    /// <summary>
    /// Spec 011 (FR-003): a permission of the operators console that guards nothing is a
    /// promise the server does not keep. The codes of the sections not built yet are let be
    /// until their own specs; the operators' own three are owed here, and since spec 012 the two
    /// of plans.
    /// </summary>
    [Fact]
    public async Task Every_operators_permission_of_this_spec_guards_a_route_and_every_code_named_is_in_the_catalogue()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        var named = Routes(factory, probes: false)
            .Select(e => (Route: Describe(e), Code: e.Metadata.GetMetadata<OperatorRouteMetadata>()?.Permission))
            .Where(x => x.Code is not null)
            .ToList();

        var strangers = named.Where(x => !OperatorPermissions.IsKnown(x.Code!)).Select(x => x.Route).ToList();
        Assert.True(strangers.Count == 0, "Not a permission of the operators' catalogue, on: " + string.Join(", ", strangers));

        var owed = new[]
            {
                OperatorPermissions.OperatorsView, OperatorPermissions.OperatorsManage, OperatorPermissions.OperatorsManageRoles,
                OperatorPermissions.BillingView, OperatorPermissions.BillingManagePlans, // spec 012: the feature catalogue
            }
            .Where(code => named.All(x => x.Code != code))
            .ToList();
        Assert.True(owed.Count == 0, "No route is guarded by: " + string.Join(", ", owed));
    }

    [Fact]
    public async Task Every_permission_a_route_names_is_in_the_catalogue()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        var strangers = Routes(factory, probes: false)
            .Select(e => (Route: Describe(e), Rule: e.Metadata.GetMetadata<AccessRuleMetadata>()))
            .Where(x => x.Rule is { Kind: not AccessRuleKind.OwnAccount } && !PermissionCatalogue.Contains(x.Rule.Permission))
            .Select(x => x.Route)
            .ToList();

        Assert.True(strangers.Count == 0, "Not a permission of the catalogue, on: " + string.Join(", ", strangers));
    }

    [Fact]
    public async Task The_routes_open_to_any_signed_in_person_are_exactly_the_written_list()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        var found = Routes(factory, probes: false)
            .Where(e => e.Metadata.GetMetadata<AccessRuleMetadata>()?.Kind == AccessRuleKind.OwnAccount)
            .Select(Describe)
            .Order(StringComparer.Ordinal);

        Assert.Equal(OwnAccountRoutes.Order(StringComparer.Ordinal), found);
    }

    [Fact]
    public async Task At_least_one_route_of_the_platform_is_guarded_by_a_permission()
    {
        await sql.EnsureMigratedAsync();
        using var factory = sql.CreateApi();

        Assert.Contains(
            Routes(factory, probes: false),
            e => Describe(e) == "GET /v1/roles"
                 && e.Metadata.GetMetadata<AccessRuleMetadata>()?.Permission == Permissions.AdminRolesView);
    }

    [Fact]
    public async Task A_route_that_declares_nothing_is_refused_for_everyone()
    {
        using var world = await RefusalWorld.CreateAsync(sql);

        // The test means something only if the route really declares nothing.
        var undeclared = Routes(world.Factory, probes: true).Single(e => e.RoutePattern.RawText == "/probe/protected");
        Assert.Null(undeclared.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Empty(undeclared.Metadata.OfType<AccessRuleMetadata>());

        using var owner = await world.Factory.SignInAsOwnerAsync(world.Clinic);
        using var refused = await owner.Browser.GetAsync(new Uri("/probe/protected", UriKind.Relative));
        var (status, code, body) = await refused.ReadAsync();

        Assert.Equal((403, "FORBIDDEN"), (status, code));
        Assert.False(body.TryGetProperty("permission", out _));

        using var nobody = world.Factory.Browser();
        using var unauthenticated = await nobody.GetAsync(new Uri("/probe/protected", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }

    [Fact]
    public void A_code_that_is_not_in_the_catalogue_cannot_be_declared()
    {
        var error = Assert.Throws<ArgumentException>(() => AccessRuleMetadata.For("no.such.code"));
        Assert.Contains("no.such.code", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_route_cannot_declare_twice()
    {
        var app = WebApplication.CreateSlimBuilder().Build();
        await using (app.ConfigureAwait(true))
        {
            app.MapGet("/twice", () => "x").OwnAccountOnly().Requires(Permissions.BookingsView);

            Assert.Throws<InvalidOperationException>(
                () => ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToList());
        }
    }
}
