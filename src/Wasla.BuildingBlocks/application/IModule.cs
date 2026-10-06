using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wasla.BuildingBlocks.Application;

/// <summary>A Wasla module: registers its services and maps its HTTP endpoints.</summary>
public interface IModule
{
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
