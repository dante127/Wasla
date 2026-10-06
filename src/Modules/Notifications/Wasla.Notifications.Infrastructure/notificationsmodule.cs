using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.BuildingBlocks.Application;

namespace Wasla.Notifications.Infrastructure;

/// <summary>Notifications module: service registration and endpoint mapping.</summary>
public sealed class NotificationsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Notifications services are registered here as the module is implemented (Phase 4+).
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/notifications");
    }
}
