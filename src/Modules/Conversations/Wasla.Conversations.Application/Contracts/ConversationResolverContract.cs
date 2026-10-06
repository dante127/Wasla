using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Application.Contracts;

/// <summary>
/// Module contract: resolve the conversation an inbound message belongs to (reopen a
/// resolved one, reuse an open one, or create a fresh conversation). Used by the
/// Channels module's inbound pipeline.
/// </summary>
public interface IConversationResolver
{
    Task<Guid> ResolveOrCreateForInboundAsync(
        TenantId tenantId,
        Guid customerId,
        Guid channelId,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
