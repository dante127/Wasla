using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Wasla.Conversations.Application;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Application.Contracts;

namespace Wasla.Conversations.Infrastructure;

/// <summary>Conversations module: service registration and endpoint mapping.</summary>
public sealed class ConversationsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ConversationsDbContext>((serviceProvider, options) => options
            .UseNpgsql(
                serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "conversations")));

        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ITagInfoProvider, TagInfoProvider>();
        services.AddScoped<IConversationsUnitOfWork, ConversationsUnitOfWork>();
        services.AddScoped<ListTagsHandler>();
        services.AddScoped<CreateTagHandler>();

        services.AddScoped<IValidator<CreateTagRequest>, CreateTagValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/conversations");
    }
}

internal sealed class ConversationsUnitOfWork(ConversationsDbContext dbContext) : IConversationsUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
