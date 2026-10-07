using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Wasla.Messages.Application;
using Wasla.Messages.Application.Abstractions;
using Wasla.Messages.Infrastructure.Repositories;

namespace Wasla.Messages.Infrastructure;

/// <summary>Messages module: service registration and endpoint mapping.</summary>
public sealed class MessagesModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MessagesDbContext>((serviceProvider, options) => options
            .UseNpgsql(
                serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "messages")));

        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));

        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessagesUnitOfWork, MessagesUnitOfWork>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<OutboxClaimer>();
        services.AddScoped<OutboxMessageDispatcher>();
        services.AddScoped<OutboxProcessor>();

        services.AddScoped<SendMessageHandler>();
        services.AddScoped<ListMessagesHandler>();
        services.AddScoped<UploadMediaHandler>();
        services.AddScoped<InboundMediaRecorder>();

        services.AddScoped<IValidator<SendMessageRequest>, SendMessageValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/messages");
    }
}

internal sealed class MessagesUnitOfWork(MessagesDbContext dbContext) : IMessagesUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
