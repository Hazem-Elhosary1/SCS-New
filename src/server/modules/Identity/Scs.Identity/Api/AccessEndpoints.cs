using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scs.BuildingBlocks.Security;
using Scs.Identity.Contracts;
using Scs.SharedKernel.Access;

namespace Scs.Identity.Api;

/// <param name="CatalogueSize">How many permissions the catalogue holds; counted, never written.</param>
internal sealed record RolesResponse(int CatalogueSize, IReadOnlyList<RoleSummary> Roles);

/// <summary>
/// The clinic's roles, for reading (contracts/access-api.openapi.yaml). The first route guarded
/// by a permission; the pages that change roles arrive with spec 027.
/// </summary>
internal static class AccessEndpoints
{
    public static IEndpointRouteBuilder MapAccess(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/roles", async (IClinicRoles roles, CancellationToken cancellation) =>
                TypedResults.Ok(new RolesResponse(PermissionCatalogue.Count, await roles.ListAsync(cancellation))))
            .Requires(Permissions.AdminRolesView)
            .WithName("ListRoles");

        return v1;
    }
}
