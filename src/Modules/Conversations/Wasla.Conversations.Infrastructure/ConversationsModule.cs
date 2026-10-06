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
using Wasla.Conversations.Application.Services;

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
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IQuickReplyRepository, QuickReplyRepository>();
        services.AddScoped<IConversationInfoProvider, ConversationInfoProvider>();
        services.AddScoped<IConversationWriter, ConversationWriter>();
        services.AddScoped<IConversationResolver, ConversationResolver>();
        services.AddScoped<IConversationsUnitOfWork, ConversationsUnitOfWork>();

        services.AddScoped<ListTagsHandler>();
        services.AddScoped<CreateTagHandler>();
        services.AddScoped<ListConversationsHandler>();
        services.AddScoped<GetConversationHandler>();
        services.AddScoped<CreateConversationHandler>();
        services.AddScoped<AssignConversationHandler>();
        services.AddScoped<ChangeConversationStatusHandler>();
        services.AddScoped<SetConversationPriorityHandler>();
        services.AddScoped<AddConversationTagsHandler>();
        services.AddScoped<RemoveConversationTagHandler>();
        services.AddScoped<AddConversationNoteHandler>();
        services.AddScoped<MarkConversationReadHandler>();
        services.AddScoped<GetConversationTimelineHandler>();
        services.AddScoped<ListQuickRepliesHandler>();
        services.AddScoped<CreateQuickReplyHandler>();
        services.AddScoped<RenderQuickReplyHandler>();

        services.AddScoped<IValidator<CreateTagRequest>, CreateTagValidator>();
        services.AddScoped<IValidator<CreateConversationRequest>, CreateConversationValidator>();
        services.AddScoped<IValidator<CreateQuickReplyRequest>, CreateQuickReplyValidator>();
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
