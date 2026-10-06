using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.BuildingBlocks.Application;

namespace Wasla.Audit.Infrastructure;

/// <summary>Audit module: service registration and endpoint mapping.</summary>
public sealed class AuditModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Audit services are registered here as the module is implemented (Phase 2+).
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/audit");
    }
}
