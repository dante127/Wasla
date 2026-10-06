using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.BuildingBlocks.Application;

namespace Wasla.Analytics.Infrastructure;

/// <summary>Analytics module: service registration and endpoint mapping.</summary>
public sealed class AnalyticsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Analytics services are registered here as the module is implemented (Phase 8+).
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/analytics");
    }
}
