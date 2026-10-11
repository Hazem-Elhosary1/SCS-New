using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Scs.BuildingBlocks.Modules;

/// <summary>
/// One module of the monolith. The host calls <see cref="AddServices"/> before the application
/// is built and <see cref="MapEndpoints"/> after, passing the <c>/v1</c> route group.
/// </summary>
public interface IModule
{
    string Name { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder v1);
}

/// <summary>
/// Maps extra endpoints at the application root once the host is built. Resolved from the
/// container, so it can be registered late (integration tests use it to add probe endpoints).
/// </summary>
public interface IEndpointContributor
{
    void Map(IEndpointRouteBuilder root);
}
