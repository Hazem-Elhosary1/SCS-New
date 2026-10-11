using Scs.BuildingBlocks.Configuration;

namespace Scs.Api;

/// <summary>
/// Technical documentation of the interface. Never reachable in production, whatever the flag
/// says (FR-019).
/// </summary>
internal static class ApiDocs
{
    public static bool IsEnabled(ScsOptions options) => options.ApiDocs.Enabled && !options.IsProduction;

    public static IServiceCollection AddScsApiDocs(this IServiceCollection services, ScsOptions options)
    {
        if (IsEnabled(options))
        {
            services.AddOpenApi();
        }

        return services;
    }

    public static IEndpointRouteBuilder MapScsApiDocs(this IEndpointRouteBuilder root, ScsOptions options)
    {
        if (IsEnabled(options))
        {
            root.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
        }

        return root;
    }
}
