using Wasla.BuildingBlocks.Domain;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Application.Abstractions;

public interface IMessageRepository
{
    Task AddAsync(Message message, CancellationToken cancellationToken);

    Task<Message?> GetByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken cancellationToken);

    Task<Message?> GetByIdAsync(TenantId tenantId, Guid messageId, CancellationToken cancellationToken);

    Task<Message?> FindByProviderMessageIdAsync(TenantId tenantId, string providerMessageId, CancellationToken cancellationToken);

    Task<(List<Message> Items, bool HasMore)> ListByConversationAsync(
        TenantId tenantId,
        Guid conversationId,
        string? beforeCursor,
        string? afterCursor,
        int limit,
        CancellationToken cancellationToken);

    Task AddMediaFileAsync(MediaFile mediaFile, CancellationToken cancellationToken);

    Task<MediaFile?> GetMediaFileAsync(TenantId tenantId, MediaFileId mediaFileId, CancellationToken cancellationToken);

    Task<List<MediaFile>> GetMediaFilesAsync(
        TenantId tenantId,
        IReadOnlyCollection<MediaFileId> mediaFileIds,
        CancellationToken cancellationToken);
}
