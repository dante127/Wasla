using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application;

/// <summary>Paged conversation timeline, newest first.</summary>
public sealed class GetConversationTimelineHandler(
    ITenantContext tenantContext,
    IConversationRepository conversations)
{
    public async Task<Result<ConversationTimelineResponse>> HandleAsync(
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<ConversationTimelineResponse>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var conversation = await conversations.GetByIdAsync(
            tenantId,
            new ConversationId(conversationId),
            cancellationToken);

        if (conversation is null)
        {
            return Result.Failure<ConversationTimelineResponse>(
                new Error("conversations.not_found", "Conversation not found."));
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize is < 1 or > 100 ? 25 : pageSize;

        var entries = conversation.Timeline
            .OrderByDescending(entry => entry.OccurredAt)
            .ToList();

        var items = entries
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(entry => new ConversationTimelineItem(
                entry.Id.Value,
                entry.Type,
                entry.Data,
                entry.ActorUserId,
                entry.OccurredAt))
            .ToList();

        return Result.Success(new ConversationTimelineResponse(items, normalizedPage, normalizedPageSize, entries.Count));
    }
}
