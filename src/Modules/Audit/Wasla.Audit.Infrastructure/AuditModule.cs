using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Audit.Application;
using Wasla.BuildingBlocks.Application;

namespace Wasla.Audit.Infrastructure;

/// <summary>Audit module: service registration and endpoint mapping.</summary>
public sealed class AuditModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetSection("Database")["ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        services.AddDbContext<AuditDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit")));

        services.AddScoped<IAuditWriter, AuditWriter>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/audit");
    }
}
