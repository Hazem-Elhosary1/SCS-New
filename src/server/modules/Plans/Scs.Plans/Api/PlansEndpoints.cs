using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.Plans.Domain;
using Scs.Plans.Infrastructure;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.Plans.Api;

internal sealed record OpsPlanRow(string? Key, int? Limit);

internal sealed record OpsDisableRequest(string? Reason);

internal sealed record OpsPlanRequest(
    string? NameAr, string? NameEn, string? Description, decimal? MonthlyEgp, decimal? AnnualEgp, decimal? MonthlyUsd,
    decimal? AnnualUsd, string? Badge, string? Note, IReadOnlyList<OpsPlanRow?>? Features, int? Version);

/// <summary>
/// The plans of the operators console (spec 013; contracts/plans-api.md). Whoever may view plans
/// reads them; whoever manages plans adds, edits, disables and enables — each asked of the server
/// on every request (SH-056). There is no delete (FR-026).
/// </summary>
internal static class PlansEndpoints
{
    public static IEndpointRouteBuilder MapPlans(this IEndpointRouteBuilder v1)
    {
        var plans = v1.MapGroup("/ops");

        plans.MapGet("/plans", ListAsync).RequiresOperator(OperatorPermissions.BillingView).WithName("OpsListPlans");
        plans.MapGet("/plans/{id:guid}", ReadAsync).RequiresOperator(OperatorPermissions.BillingView).WithName("OpsGetPlan");
        plans.MapGet("/plans/grid", GridAsync).RequiresOperator(OperatorPermissions.BillingView).WithName("OpsPlanGrid");

        plans.MapPost("/plans", CreateAsync)
            .RequiresOperator(OperatorPermissions.BillingManagePlans).Records(AuditActions.PlanCreated)
            .WithName("OpsCreatePlan");

        plans.MapGet("/plans/{id:guid}/grid", EditGridAsync).RequiresOperator(OperatorPermissions.BillingView).WithName("OpsPlanEditGrid");
        plans.MapPut("/plans/{id:guid}", EditAsync)
            .RequiresOperator(OperatorPermissions.BillingManagePlans).Records(AuditActions.PlanUpdated)
            .WithName("OpsEditPlan");

        plans.MapPost("/plans/{id:guid}/disable", DisableAsync)
            .RequiresOperator(OperatorPermissions.BillingManagePlans).Records(AuditActions.PlanDisabled)
            .WithName("OpsDisablePlan");
        plans.MapPost("/plans/{id:guid}/enable", EnableAsync)
            .RequiresOperator(OperatorPermissions.BillingManagePlans).Records(AuditActions.PlanEnabled)
            .WithName("OpsEnablePlan");

        return v1;
    }

    private static async Task<IResult> DisableAsync(Guid id, OpsDisableRequest? request, PlanBook book, CancellationToken cancellation)
    {
        await book.DisableAsync(id, request?.Reason, cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> EnableAsync(Guid id, PlanBook book, CancellationToken cancellation)
    {
        await book.EnableAsync(id, cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> EditGridAsync(Guid id, PlanBook book, CancellationToken cancellation) =>
        await book.GridAsync(id, cancellation) is { } grid ? TypedResults.Ok(grid) : throw NotFound();

    /// <summary>The code is the plan's for good: one in the body is never read (ق-٤٦).</summary>
    private static async Task<IResult> EditAsync(Guid id, OpsPlanRequest? request, PlanBook book, CancellationToken cancellation)
    {
        await book.EditAsync(id, Input(request), cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GridAsync(PlanBook book, CancellationToken cancellation) =>
        TypedResults.Ok(await book.GridAsync(null, cancellation));

    /// <summary>The code is the platform's to derive: one sent in the body is never read (ق-٤٦).</summary>
    private static async Task<IResult> CreateAsync(OpsPlanRequest? request, PlanBook book, CancellationToken cancellation)
    {
        var made = await book.AddAsync(Input(request), cancellation);
        return TypedResults.Created($"/v1/ops/plans/{made.Id}", made);
    }

    private static PlanInput Input(OpsPlanRequest? request) =>
        new(
            request?.NameAr, request?.NameEn, request?.Description, request?.MonthlyEgp, request?.AnnualEgp, request?.MonthlyUsd,
            request?.AnnualUsd, request?.Badge, request?.Note,
            [.. (request?.Features ?? []).Select(row => new GridRow(row?.Key, row?.Limit))], request?.Version);

    private static async Task<IResult> ListAsync(IOperatorAccess access, PlanBook book, CancellationToken cancellation)
    {
        var manages = await access.HasAsync(OperatorPermissions.BillingManagePlans, cancellation);
        return TypedResults.Ok(await book.ListAsync(manages, cancellation));
    }

    private static async Task<IResult> ReadAsync(Guid id, PlanBook book, CancellationToken cancellation) =>
        await book.ReadAsync(id, cancellation) is { } found ? TypedResults.Ok(found) : throw NotFound();

    private static ScsProblemException NotFound() => new(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
}
