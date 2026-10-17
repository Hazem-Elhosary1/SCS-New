using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Plans;

/// <summary>
/// What the established clinic's plan says, as the shared gate and the limit helper need it
/// (spec 014). The subscriptions module answers it from its one entitlement read; nothing here
/// knows how.
/// </summary>
public interface IPlanAnswers
{
    /// <summary>True when every key is a built feature the clinic's plan does not include. An upcoming feature is never withheld (ق-٤٩).</summary>
    Task<bool> IsWithheldAsync(IReadOnlyList<string> featureKeys, CancellationToken cancellation);

    /// <summary>The clinic's limit; null for no limit, or for a feature nothing enforces yet; 0 for a key the catalogue does not know.</summary>
    Task<int?> LimitOfAsync(string featureKey, CancellationToken cancellation);

    /// <summary>The feature's name in both languages, for the words of a refusal.</summary>
    Task<TitleValue> NameOfAsync(string featureKey, CancellationToken cancellation);
}

/// <summary>The feature, or features (any one of which is enough), a clinic route belongs to.</summary>
public sealed class PlanFeatureMetadata(IReadOnlyList<string> keys)
{
    public IReadOnlyList<string> Keys { get; } = keys;
}

/// <summary>The two refusals of a plan, with one code and one shape each (SH-014, SH-017, ق-٤٠).</summary>
public static class PlanRefusals
{
    public static ScsProblemException NotIncluded(string featureKey) =>
        new(StatusCodes.Status403Forbidden, ErrorCodes.PlanFeatureNotIncluded, $"The plan does not include {featureKey}.")
        {
            Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["feature"] = featureKey },
        };

    /// <param name="name">The feature's catalogue name, for «{feature}» in the words.</param>
    public static ScsProblemException LimitReached(string featureKey, int limit, TitleValue name) =>
        new(StatusCodes.Status409Conflict, ErrorCodes.PlanLimitReached, $"The plan's limit of {featureKey} ({limit}) is reached.")
        {
            Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["feature"] = featureKey, ["limit"] = limit },
            TitleValues = new Dictionary<string, TitleValue>(StringComparer.Ordinal)
            {
                ["feature"] = name,
                ["limit"] = TitleValue.Both(limit.ToString("#,0", CultureInfo.InvariantCulture)),
            },
        };
}

public static class PlanGateExtensions
{
    /// <summary>The route belongs to this feature: refused for a clinic whose plan does not include it, once it is built.</summary>
    public static TBuilder RequiresFeature<TBuilder>(this TBuilder builder, string featureKey)
        where TBuilder : IEndpointConventionBuilder => builder.Declare([featureKey]);

    /// <summary>The route belongs to any of these features: refused only when the plan includes none of them.</summary>
    public static TBuilder RequiresAnyFeature<TBuilder>(this TBuilder builder, params string[] featureKeys)
        where TBuilder : IEndpointConventionBuilder => builder.Declare(featureKeys);

    private static TBuilder Declare<TBuilder>(this TBuilder builder, string[] keys)
        where TBuilder : IEndpointConventionBuilder
    {
        if (keys.Length == 0)
        {
            throw new ArgumentException("A route names at least one feature.", nameof(keys));
        }

        builder.Add(endpoint =>
        {
            if (endpoint.Metadata.OfType<PlanFeatureMetadata>().Any())
            {
                throw new InvalidOperationException($"The route '{endpoint.DisplayName}' names its feature more than once.");
            }

            endpoint.Metadata.Add(new PlanFeatureMetadata(keys));
        });
        return builder;
    }

    /// <summary>
    /// Refuses a clinic route whose feature the clinic's plan does not include (spec 014, FR-008),
    /// before its handler runs: nothing has been read or changed by then. Placed right after the
    /// access gate, so a person who may not call the route is told so and never about the plan
    /// (FR-009). An operators' route is never a plan's.
    /// </summary>
    public static IApplicationBuilder UseScsPlanGate(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            var feature = endpoint?.Metadata.GetMetadata<PlanFeatureMetadata>();
            if (feature is null || endpoint!.Metadata.GetMetadata<OperatorRouteMetadata>() is not null)
            {
                await next(context);
                return;
            }

            var answers = context.RequestServices.GetRequiredService<IPlanAnswers>();
            if (!await answers.IsWithheldAsync(feature.Keys, context.RequestAborted))
            {
                await next(context);
                return;
            }

            await ProblemWriter.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                ErrorCodes.PlanFeatureNotIncluded,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["feature"] = feature.Keys[0] });
        });
}
