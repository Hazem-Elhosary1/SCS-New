using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.Plans.Infrastructure;
using Scs.SharedKernel;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.Plans.Api;

internal sealed record OpsFeatureRequest(
    string? Key, string? NameAr, string? NameEn, string? DescriptionAr, string? DescriptionEn, string? Group, string? Kind,
    string? UnitAr, string? UnitEn, int? Baseline, string? Specialty, string? Status, bool? IsSold, int? Version);

internal sealed record OpsFeatureCreated(string Key);

/// <summary>
/// The feature catalogue of the operators console (spec 012; contracts/features-api.md). Whoever
/// may view plans reads it; whoever manages plans adds, edits, retires and sells again — each
/// asked of the server on every request (SH-056).
/// </summary>
internal static class FeaturesEndpoints
{
    public static IEndpointRouteBuilder MapFeatures(this IEndpointRouteBuilder v1)
    {
        var features = v1.MapGroup("/ops");

        features.MapGet("/features", ListAsync).RequiresOperator(OperatorPermissions.BillingView).WithName("OpsListFeatures");
        features.MapGet("/features/{key}", ReadAsync).RequiresOperator(OperatorPermissions.BillingView).WithName("OpsGetFeature");
        features.MapGet("/features/{key}/plans", PlansAsync).RequiresOperator(OperatorPermissions.BillingView).WithName("OpsFeaturePlans");

        features.MapPost("/features", CreateAsync)
            .RequiresOperator(OperatorPermissions.BillingManagePlans).Records(AuditActions.FeatureCreated)
            .WithName("OpsCreateFeature");

        features.MapPut("/features/{key}", EditAsync)
            .RequiresOperator(OperatorPermissions.BillingManagePlans).Records(AuditActions.FeatureUpdated)
            .WithName("OpsEditFeature");

        features.MapPost("/features/{key}/retire", RetireAsync)
            .RequiresOperator(OperatorPermissions.BillingManagePlans).Records(AuditActions.FeatureRetired)
            .WithName("OpsRetireFeature");

        features.MapPost("/features/{key}/resume", ResumeAsync)
            .RequiresOperator(OperatorPermissions.BillingManagePlans).Records(AuditActions.FeatureResumed)
            .WithName("OpsResumeFeature");

        return v1;
    }

    private static async Task<IResult> ListAsync(IOperatorAccess access, FeatureCatalogue catalogue, CancellationToken cancellation)
    {
        var manages = await access.HasAsync(OperatorPermissions.BillingManagePlans, cancellation);
        return TypedResults.Ok(await catalogue.ListAsync(manages, cancellation));
    }

    private static async Task<IResult> ReadAsync(string key, FeatureCatalogue catalogue, CancellationToken cancellation) =>
        await catalogue.ReadAsync(key, cancellation) is { } found ? TypedResults.Ok(found) : throw NotFound();

    private static async Task<IResult> PlansAsync(string key, FeatureCatalogue catalogue, CancellationToken cancellation) =>
        await catalogue.PlansOfAsync(key, cancellation) is { } used ? TypedResults.Ok(used) : throw NotFound();

    private static async Task<IResult> CreateAsync(OpsFeatureRequest? request, FeatureCatalogue catalogue, CancellationToken cancellation)
    {
        var key = await catalogue.AddAsync(Input(request, keyed: true), cancellation);
        return TypedResults.Created($"/v1/ops/features/{key}", new OpsFeatureCreated(key));
    }

    /// <summary>The key is the address's: one in the body is never read (ق-٤٦).</summary>
    private static async Task<IResult> EditAsync(string key, OpsFeatureRequest? request, FeatureCatalogue catalogue, CancellationToken cancellation)
    {
        await catalogue.EditAsync(key, Input(request, keyed: false), cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> RetireAsync(string key, FeatureCatalogue catalogue, CancellationToken cancellation)
    {
        await catalogue.RetireAsync(key, cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ResumeAsync(string key, FeatureCatalogue catalogue, CancellationToken cancellation)
    {
        await catalogue.ResumeAsync(key, cancellation);
        return TypedResults.NoContent();
    }

    private static ScsProblemException NotFound() => new(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    private static FeatureInput Input(OpsFeatureRequest? request, bool keyed) =>
        new(
            keyed ? request?.Key : null, request?.NameAr, request?.NameEn, request?.DescriptionAr, request?.DescriptionEn, request?.Group,
            request?.Kind, request?.UnitAr, request?.UnitEn, request?.Baseline, request?.Specialty, request?.Status, request?.IsSold,
            request?.Version);
}
