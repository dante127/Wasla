using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.BuildingBlocks.Application;
using Wasla.Tenancy.Application;
using Wasla.Tenancy.Application.Abstractions;
using Wasla.Tenancy.Application.Contracts;

namespace Wasla.Tenancy.Infrastructure;

/// <summary>Tenancy module: service registration and endpoint mapping.</summary>
public sealed class TenancyModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetSection("Database")["ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        services.AddDbContext<TenancyDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenancy")));

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantInfoProvider, TenantInfoProvider>();
        services.AddScoped<ITenancyUnitOfWork, TenancyUnitOfWork>();
        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<GetCurrentTenantHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/tenancy");
    }
}

internal sealed class TenancyUnitOfWork(TenancyDbContext dbContext) : ITenancyUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
