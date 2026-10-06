using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.BuildingBlocks.Application;

namespace Wasla.Teams.Infrastructure;

/// <summary>Teams module: service registration and endpoint mapping.</summary>
public sealed class TeamsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Teams services are registered here as the module is implemented (Phase 2+).
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/teams");
    }
}
