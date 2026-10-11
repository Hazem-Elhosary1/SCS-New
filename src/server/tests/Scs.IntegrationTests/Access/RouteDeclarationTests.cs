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
                + e.Metadata.OfType<AccessRuleMetadata>().Count() != 1)
            .Select(Describe)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Say who may call each of these — AllowAnonymous(), OwnAccountOnly(), Requires(...) or RequiresOrOwner(...), and only one: "
            + string.Join(", ", offenders));
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
