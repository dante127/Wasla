using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Paging;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Application.Contracts;

namespace Wasla.Conversations.Application;

/// <summary>The unified inbox query: cursor-paginated, newest activity first.</summary>
public sealed class ListConversationsHandler(
    ITenantContext tenantContext,
    IConversationRepository conversations,
    ICustomerInfoProvider customers,
    IChannelInfoProvider channels,
    ITagInfoProvider tags)
{
    public async Task<Result<ConversationListResponse>> HandleAsync(
        ConversationListQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<ConversationListResponse>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        if (!string.IsNullOrWhiteSpace(query.Cursor)
            && !CursorCodec.TryDecode(query.Cursor, out _, out _))
        {
            return Result.Failure<ConversationListResponse>(
                new Error("conversations.invalid_cursor", "The pagination cursor is invalid."));
        }

        var (items, hasMore) = await conversations.ListAsync(tenantId, query, cancellationToken);

        var customerLookup = await customers.GetManyAsync(
            tenantId,
            items.Select(item => item.CustomerId).Distinct().ToList(),
            cancellationToken);
        var channelLookup = await channels.GetManyAsync(
            tenantId,
            items.Select(item => item.ChannelId).Distinct().ToList(),
            cancellationToken);
        var tagLookup = await tags.GetManyAsync(
            tenantId,
            items.SelectMany(item => item.Tags.Select(tag => tag.TagId)).Distinct().ToList(),
            cancellationToken);

        var list = items
            .Select(item => ConversationMapper.ToListItem(item, customerLookup, channelLookup, tagLookup))
            .ToList();

        string? nextCursor = null;

        if (hasMore && items.Count > 0)
        {
            var last = items[^1];
            nextCursor = CursorCodec.Encode(last.LastMessageAt ?? last.CreatedAt, last.Id.Value);
        }

        return Result.Success(new ConversationListResponse(list, nextCursor));
    }
}