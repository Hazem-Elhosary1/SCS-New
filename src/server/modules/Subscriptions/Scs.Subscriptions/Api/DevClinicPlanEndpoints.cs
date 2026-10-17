using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scs.BuildingBlocks.Configuration;
using Scs.BuildingBlocks.Errors;
using Scs.BuildingBlocks.Security;
using Scs.BuildingBlocks.Tenancy;
using Scs.Plans.Contracts;
using Scs.SharedKernel;
using Scs.Subscriptions.Contracts;

namespace Scs.Subscriptions.Api;

/// <summary>Puts a seeded clinic on a plan, or on none. A developer's machine only (spec 014, research R15).</summary>
public sealed record DevClinicPlanRequest(string? Clinic, string? PlanCode);

internal static class DevClinicPlanEndpoints
{
    public static IEndpointRouteBuilder MapDevClinicPlan(this IEndpointRouteBuilder v1)
    {
        if (v1.ServiceProvider.GetRequiredService<IOptions<ScsOptions>>().Value.IsLocal)
        {
            // There is no screen that gives a clinic a plan until spec 015, and the plan's limits
            // have to be walked somehow (FR-031).
            v1.MapPost("/dev/clinic-plan", SetAsync)
                .AllowAnonymous().HandlesOwnSession()
                .RecordsNothing("local and test environments only")
                .WithName("DevSetClinicPlan");
        }

        return v1;
    }

    private static async Task<IResult> SetAsync(
        DevClinicPlanRequest? request, IClinicLocator locator, IPlanBook plans, IClinicPlans clinicPlans,
        CancellationToken cancellation)
    {
        var location = string.IsNullOrWhiteSpace(request?.Clinic)
            ? null
            : await locator.LocateAsync(request.Clinic, cancellation);
        if (location is null)
        {
            throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "No such clinic.");
        }

        Guid? planId = null;
        if (!string.IsNullOrWhiteSpace(request!.PlanCode))
        {
            var plan = (await plans.AssignableAsync(cancellation))
                       .FirstOrDefault(p => string.Equals(p.Code, request.PlanCode, StringComparison.Ordinal))
                       ?? throw new ScsProblemException(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "No such active plan.");
            planId = plan.Id;
        }

        // The locator has established the clinic: from here on, ordinary isolation.
        await clinicPlans.SetAsync(planId, cancellation);
        return TypedResults.NoContent();
    }
}
