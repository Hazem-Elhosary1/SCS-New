using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tracing;
using Scs.Operators.Infrastructure;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.Operators.Api;

/// <param name="TwoStep">Read and not kept: the code by email is asked of every operator (FR-040).</param>
internal sealed record OpsOperatorRequest(
    [property: PersonalData] string? FullName,
    [property: PersonalData] string? Email,
    Guid? RoleId,
    [property: PersonalData] string? Mobile,
    string? Department,
    string? JobTitle,
    string? Language,
    bool? TwoStep,
    bool? SendInvitation,
    string? Note,
    string? Version)
{
    public override string ToString() => nameof(OpsOperatorRequest);
}

internal sealed record OpsSuspendRequest(string? Reason);

internal sealed record OpsCreated(Guid Id);

/// <summary>
/// The team of the operators console (spec 011; contracts/operators-roles-api.md §2 and §3).
/// Reading needs <c>operators.view</c>; every change needs <c>operators.manage</c>, and the gate
/// answers before any of these runs. None returns a link, a token or a password.
/// </summary>
internal static class OperatorsEndpoints
{
    public static IEndpointRouteBuilder MapOperatorsTeam(this IEndpointRouteBuilder v1)
    {
        var team = v1.MapGroup("/ops");

        team.MapGet("/operators", ListAsync).RequiresOperator(OperatorPermissions.OperatorsView).WithName("OpsListOperators");
        team.MapGet("/operators/{id:guid}", ReadAsync).RequiresOperator(OperatorPermissions.OperatorsView).WithName("OpsGetOperator");

        team.MapPost("/operators", InviteAsync)
            .RequiresOperator(OperatorPermissions.OperatorsManage).Records(AuditActions.OperatorInvited)
            .WithName("OpsInviteOperator");

        team.MapPut("/operators/{id:guid}", EditAsync)
            .RequiresOperator(OperatorPermissions.OperatorsManage)
            .Records(AuditActions.OperatorEdited, AuditActions.OperatorRoleChanged)
            .WithName("OpsEditOperator");

        team.MapPost("/operators/{id:guid}/suspend", SuspendAsync)
            .RequiresOperator(OperatorPermissions.OperatorsManage).Records(AuditActions.OperatorSuspended)
            .WithName("OpsSuspendOperator");

        team.MapPost("/operators/{id:guid}/reactivate", ReactivateAsync)
            .RequiresOperator(OperatorPermissions.OperatorsManage).Records(AuditActions.OperatorReactivated)
            .WithName("OpsReactivateOperator");

        team.MapPost("/operators/{id:guid}/password-link", SendLinkAsync)
            .RequiresOperator(OperatorPermissions.OperatorsManage).Records(AuditActions.OperatorLinkSent)
            .WithName("OpsSendOperatorLink");

        team.MapPost("/operators/{id:guid}/unlock", UnlockAsync)
            .RequiresOperator(OperatorPermissions.OperatorsManage).Records(AuditActions.OperatorUnlocked)
            .WithName("OpsUnlockOperator");

        return v1;
    }

    private static async Task<IResult> ListAsync(
        string? search, string? state, Guid? roleId, int? page, OperatorsDirectory directory, CancellationToken cancellation) =>
        TypedResults.Ok(await directory.ListAsync(search, state, roleId, page ?? 1, cancellation));

    private static async Task<IResult> ReadAsync(
        Guid id, ICurrentOperator current, HttpContext context, OperatorsDirectory directory, CancellationToken cancellation) =>
        await directory.ReadAsync(id, current.OperatorId, Language(context), cancellation) is { } found
            ? TypedResults.Ok(found)
            : throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    private static async Task<IResult> InviteAsync(
        OpsOperatorRequest? request, ICurrentOperator current, OperatorManagement management, CancellationToken cancellation)
    {
        var id = await management.InviteAsync(Input(request), current.OperatorId, cancellation);
        return TypedResults.Created($"/v1/ops/operators/{id}", new OpsCreated(id));
    }

    private static async Task<IResult> EditAsync(
        Guid id, OpsOperatorRequest? request, ICurrentOperator current, HttpContext context, OperatorManagement management,
        CancellationToken cancellation)
    {
        await management.EditAsync(id, Input(request), current.OperatorId, Origin(context), cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SuspendAsync(
        Guid id, OpsSuspendRequest? request, ICurrentOperator current, HttpContext context, OperatorManagement management,
        CancellationToken cancellation)
    {
        await management.SuspendAsync(id, request?.Reason, current.OperatorId, Origin(context), cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ReactivateAsync(Guid id, OperatorManagement management, CancellationToken cancellation)
    {
        await management.ReactivateAsync(id, cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SendLinkAsync(Guid id, OperatorManagement management, CancellationToken cancellation)
    {
        await management.SendLinkAsync(id, cancellation);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<IResult> UnlockAsync(Guid id, OperatorManagement management, CancellationToken cancellation)
    {
        await management.UnlockAsync(id, cancellation);
        return TypedResults.NoContent();
    }

    /// <summary>The email is taken only when adding: an edit that carries one changes nothing of it (ق-59).</summary>
    private static OperatorInput Input(OpsOperatorRequest? request) => new(
        request?.FullName, request?.Email, request?.RoleId, request?.Mobile, request?.Department, request?.JobTitle,
        request?.Language, request?.SendInvitation ?? true, request?.Note, request?.Version);

    internal static string Language(HttpContext context) =>
        context.Request.Headers.AcceptLanguage.ToString().StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";

    internal static OpsOrigin Origin(HttpContext context) => new(
        context.Connection.RemoteIpAddress?.ToString(),
        context.Request.Headers.UserAgent.ToString(),
        TraceIdMiddleware.Get(context));
}
