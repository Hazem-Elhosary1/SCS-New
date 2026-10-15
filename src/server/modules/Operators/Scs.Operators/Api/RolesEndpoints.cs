using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.Operators.Infrastructure;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.Operators.Api;

internal sealed record OpsRoleRequest(
    string? Name, string? Description, IReadOnlyList<string>? Codes, Guid? BasedOnId, string? Note, string? Version);

internal sealed record OpsDeleteRoleRequest(string? Note);

internal sealed record OpsRoleOptions(IReadOnlyList<RoleOption> Items);

internal sealed record OpsCatalogue(IReadOnlyList<CatalogueGroup> Groups);

/// <summary>
/// The roles of the operators console (spec 011; contracts/operators-roles-api.md §2 and §4).
/// Whoever sees the team sees the list of roles; the role picker is for whoever manages
/// operators; the form's reads and every write are for whoever manages roles.
/// </summary>
internal static class RolesEndpoints
{
    public static IEndpointRouteBuilder MapOperatorRoles(this IEndpointRouteBuilder v1)
    {
        var roles = v1.MapGroup("/ops");

        roles.MapGet("/roles", ListAsync).RequiresOperator(OperatorPermissions.OperatorsView).WithName("OpsListRoles");
        roles.MapGet("/roles/options", OptionsAsync).RequiresOperator(OperatorPermissions.OperatorsManage).WithName("OpsRoleOptions");
        roles.MapGet("/roles/{id:guid}", ReadAsync).RequiresOperator(OperatorPermissions.OperatorsManageRoles).WithName("OpsGetRole");

        roles.MapPost("/roles", CreateAsync)
            .RequiresOperator(OperatorPermissions.OperatorsManageRoles).Records(AuditActions.OperatorRoleCreated)
            .WithName("OpsCreateRole");

        roles.MapPut("/roles/{id:guid}", EditAsync)
            .RequiresOperator(OperatorPermissions.OperatorsManageRoles).Records(AuditActions.OperatorRoleEdited)
            .WithName("OpsEditRole");

        roles.MapPost("/roles/{id:guid}/delete", DeleteAsync)
            .RequiresOperator(OperatorPermissions.OperatorsManageRoles).Records(AuditActions.OperatorRoleDeleted)
            .WithName("OpsDeleteRole");

        v1.MapGet("/ops/permissions", CatalogueAsync)
            .RequiresOperator(OperatorPermissions.OperatorsManageRoles).WithName("OpsPermissionCatalogue");

        return v1;
    }

    private static async Task<IResult> ListAsync(Roles roles, CancellationToken cancellation) =>
        TypedResults.Ok(await roles.ListAsync(cancellation));

    private static async Task<IResult> OptionsAsync(ICurrentOperator current, Roles roles, CancellationToken cancellation) =>
        TypedResults.Ok(new OpsRoleOptions(await roles.OptionsAsync(current.OperatorId, cancellation)));

    private static async Task<IResult> ReadAsync(Guid id, ICurrentOperator current, Roles roles, CancellationToken cancellation) =>
        await roles.ReadAsync(id, current.OperatorId, cancellation) is { } found
            ? TypedResults.Ok(found)
            : throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    private static async Task<IResult> CatalogueAsync(ICurrentOperator current, Roles roles, CancellationToken cancellation) =>
        TypedResults.Ok(new OpsCatalogue(await roles.CatalogueAsync(current.OperatorId, cancellation)));

    private static async Task<IResult> CreateAsync(
        OpsRoleRequest? request, ICurrentOperator current, Roles roles, CancellationToken cancellation)
    {
        var id = await roles.CreateAsync(Input(request), current.OperatorId, cancellation);
        return TypedResults.Created($"/v1/ops/roles/{id}", new OpsCreated(id));
    }

    private static async Task<IResult> EditAsync(
        Guid id, OpsRoleRequest? request, ICurrentOperator current, Roles roles, CancellationToken cancellation)
    {
        await roles.EditAsync(id, Input(request), current.OperatorId, cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid id, OpsDeleteRoleRequest? request, Roles roles, CancellationToken cancellation)
    {
        await roles.DeleteAsync(id, request?.Note, cancellation);
        return TypedResults.NoContent();
    }

    private static RoleInput Input(OpsRoleRequest? request) =>
        new(request?.Name, request?.Description, request?.Codes, request?.BasedOnId, request?.Note, request?.Version);
}
