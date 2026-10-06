using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.BuildingBlocks.Application;

namespace Wasla.Messages.Infrastructure;

/// <summary>Messages module: service registration and endpoint mapping.</summary>
public sealed class MessagesModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Messages services are registered here as the module is implemented (Phase 4+).
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/messages");
    }
}
