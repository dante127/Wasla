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
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.Channels.Application.Services;
using Wasla.Channels.Infrastructure.Adapters;
using Wasla.Channels.Infrastructure.Adapters.Telegram;
using Wasla.Channels.Infrastructure.Adapters.WhatsApp;
using Wasla.Channels.Infrastructure.Repositories;
using Wasla.Channels.Infrastructure.Security;
using Wasla.BuildingBlocks.Application.ChannelAdapters;

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

        services.AddOptions<WhatsAppOptions>().Bind(configuration.GetSection(WhatsAppOptions.SectionName));

        services.AddHttpClient<WhatsAppCloudAdapter>((serviceProvider, client) =>
        {
            var whatsApp = serviceProvider.GetRequiredService<IOptions<WhatsAppOptions>>().Value;

            client.BaseAddress = new Uri(whatsApp.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, whatsApp.TimeoutSeconds));
        });

        services.AddScoped<IChannelAdapter>(serviceProvider => serviceProvider.GetRequiredService<WhatsAppCloudAdapter>());
        services.AddScoped<IChannelConnectionVerifier>(serviceProvider => serviceProvider.GetRequiredService<WhatsAppCloudAdapter>());

        services.AddOptions<TelegramOptions>().Bind(configuration.GetSection(TelegramOptions.SectionName));

        services.AddHttpClient<TelegramBotAdapter>((serviceProvider, client) =>
        {
            var telegram = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;

            client.BaseAddress = new Uri(telegram.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, telegram.TimeoutSeconds));
        });

        services.AddHttpClient<TelegramWebhookRegistrar>((serviceProvider, client) =>
        {
            var telegram = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;

            client.BaseAddress = new Uri(telegram.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, telegram.TimeoutSeconds));
        });

        services.AddScoped<IChannelAdapter>(serviceProvider => serviceProvider.GetRequiredService<TelegramBotAdapter>());
        services.AddScoped<IChannelConnectionVerifier>(serviceProvider => serviceProvider.GetRequiredService<TelegramBotAdapter>());
        services.AddScoped<IChannelWebhookRegistrar>(serviceProvider => serviceProvider.GetRequiredService<TelegramWebhookRegistrar>());
        services.AddScoped<IChannelWebhookRegistrarRegistry, ChannelWebhookRegistrarRegistry>();
        services.AddScoped<IChannelAdapterRegistry, ChannelAdapterRegistry>();
        services.AddScoped<IChannelConnectionVerifierRegistry, ChannelConnectionVerifierRegistry>();
        services.AddScoped<IChannelRoutingProvider, ChannelRoutingProvider>();
        services.AddScoped<IChannelCredentialStore, ChannelCredentialStore>();
        services.AddScoped<IInboxRepository, InboxRepository>();
        services.AddScoped<IWebhookRequestHandler, WebhookRequestHandler>();
        services.AddScoped<InboxClaimer>();
        services.AddScoped<IInboundEventApplier, InboundEventApplier>();
        services.AddScoped<InboxProcessor>();
        services.AddScoped<ConnectChannelHandler>();
        services.AddScoped<DisconnectChannelHandler>();
        services.AddScoped<IValidator<ConnectChannelRequest>, ConnectChannelValidator>();



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
