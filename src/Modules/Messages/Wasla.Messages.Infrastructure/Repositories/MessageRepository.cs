using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Application.Paging;
using Wasla.BuildingBlocks.Domain;
using Wasla.Messages.Application.Abstractions;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Infrastructure.Repositories;

public sealed class MessageRepository(MessagesDbContext dbContext) : IMessageRepository
{
    public async Task AddAsync(Message message, CancellationToken cancellationToken) =>
        await dbContext.Messages.AddAsync(message, cancellationToken);

    public Task<Message?> GetByIdempotencyKeyAsync(
        TenantId tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.Messages
            .Include(message => message.Attachments)
            .FirstOrDefaultAsync(
                message => message.TenantId == tenantId && message.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<Message?> GetByIdAsync(TenantId tenantId, Guid messageId, CancellationToken cancellationToken) =>
        dbContext.Messages
            .Include(message => message.Attachments)
            .FirstOrDefaultAsync(
                message => message.TenantId == tenantId && message.Id == new MessageId(messageId),
                cancellationToken);

    public Task<Message?> FindByProviderMessageIdAsync(TenantId tenantId, string providerMessageId, CancellationToken cancellationToken) =>
        dbContext.Messages
            .Include(message => message.Attachments)
            .FirstOrDefaultAsync(
                message => message.TenantId == tenantId && message.ProviderMessageId == providerMessageId,
                cancellationToken);

    public async Task<(List<Message> Items, bool HasMore)> ListByConversationAsync(
        TenantId tenantId,
        Guid conversationId,
        string? beforeCursor,
        string? afterCursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var queryable = dbContext.Messages
            .AsNoTracking()
            .Include(message => message.Attachments)
            .Where(message => message.TenantId == tenantId && message.ConversationId == conversationId);

        if (CursorCodec.TryDecode(beforeCursor, out var beforeTimestamp, out _))
        {
            queryable = queryable.Where(message =>
                message.CreatedAt < beforeTimestamp);
        }
        else if (CursorCodec.TryDecode(afterCursor, out var afterTimestamp, out _))
        {
            queryable = queryable.Where(message =>
                message.CreatedAt > afterTimestamp);
        }

        var items = await queryable
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return (items, hasMore);
    }

    public async Task AddMediaFileAsync(MediaFile mediaFile, CancellationToken cancellationToken) =>
        await dbContext.MediaFiles.AddAsync(mediaFile, cancellationToken);

    public Task<MediaFile?> GetMediaFileAsync(
        TenantId tenantId,
        MediaFileId mediaFileId,
        CancellationToken cancellationToken) =>
        dbContext.MediaFiles.FirstOrDefaultAsync(
            media => media.TenantId == tenantId && media.Id == mediaFileId,
            cancellationToken);

    public async Task<List<MediaFile>> GetMediaFilesAsync(
        TenantId tenantId,
        IReadOnlyCollection<MediaFileId> mediaFileIds,
        CancellationToken cancellationToken)
    {
        var ids = mediaFileIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.MediaFiles
            .Where(media => media.TenantId == tenantId && ids.Contains(media.Id))
            .ToListAsync(cancellationToken);
    }
}
