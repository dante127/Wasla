using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Wasla.Channels.Application;
using Wasla.Channels.Application.Abstractions;
using Wasla.Channels.Application.Contracts;

namespace Wasla.Channels.Infrastructure;

/// <summary>Channels module: service registration and endpoint mapping.</summary>
public sealed class ChannelsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ChannelsDbContext>((serviceProvider, options) => options
            .UseNpgsql(
                serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "channels")));

        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IChannelInfoProvider, ChannelInfoProvider>();
        services.AddScoped<IChannelsUnitOfWork, ChannelsUnitOfWork>();
        services.AddScoped<ListChannelsHandler>();
        services.AddScoped<CreateChannelHandler>();

        services.AddScoped<IValidator<CreateChannelRequest>, CreateChannelValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/channels");
    }
}

internal sealed class ChannelsUnitOfWork(ChannelsDbContext dbContext) : IChannelsUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
