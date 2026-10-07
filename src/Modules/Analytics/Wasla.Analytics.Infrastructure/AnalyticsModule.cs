using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Analytics.Application;
using Wasla.Analytics.Application.Abstractions;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Configuration;

namespace Wasla.Analytics.Infrastructure;

/// <summary>Analytics module: service registration and endpoint mapping.</summary>
public sealed class AnalyticsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AnalyticsDbContext>((serviceProvider, options) => options
            .UseNpgsql(
                serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "analytics")));

        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IAnalyticsUnitOfWork, AnalyticsUnitOfWork>();
        services.AddScoped<AnalyticsRecomputeService>();
        services.AddScoped<RecomputeAnalyticsHandler>();
        services.AddScoped<GetAnalyticsOverviewHandler>();
        services.AddScoped<GetAnalyticsAgentReportHandler>();
        services.AddScoped<GetAnalyticsChannelReportHandler>();
        services.AddScoped<TenantAnalyticsRecomputer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/analytics");
    }
}

internal sealed class AnalyticsUnitOfWork(AnalyticsDbContext dbContext) : IAnalyticsUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
