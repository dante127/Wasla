using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Application.Abstractions;

/// <summary>Encrypted credential storage for channel provider secrets.</summary>
public interface IChannelCredentialStore
{
    Task<string?> GetAsync(TenantId tenantId, Guid channelId, string key, CancellationToken cancellationToken);

    Task SetAsync(TenantId tenantId, Guid channelId, string key, string value, CancellationToken cancellationToken);

    Task RemoveAsync(TenantId tenantId, Guid channelId, string key, CancellationToken cancellationToken);
}

/// <summary>Minimal claim descriptor for an inbox row picked up by a worker.</summary>
public sealed record InboxClaim(Guid Id, TenantId TenantId);

public interface IInboxRepository
{
    Task AddAsync(InboxEvent inboxEvent, CancellationToken cancellationToken);

    Task<InboxEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByBodyHashAsync(Guid channelId, string bodyHash, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims a batch of due rows (FOR UPDATE SKIP LOCKED; runs without a
    /// tenant filter because it crosses tenants). Stale in-flight rows are reclaimable.
    /// </summary>
    Task<List<InboxClaim>> ClaimPendingAsync(int batchSize, CancellationToken cancellationToken);
}

/// <summary>Provider-neutral essentials of an inbound webhook request.</summary>
public sealed record WebhookRequest(
    Guid ChannelId,
    string HttpMethod,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Query,
    byte[] Body);

/// <summary>Provider-neutral webhook response (status + body + content type).</summary>
public sealed record WebhookResult(int StatusCode, string? Body, string ContentType = "text/plain");

/// <summary>Handles the whole webhook round trip: verify → persist raw → 2xx fast path.</summary>
public interface IWebhookRequestHandler
{
    Task<WebhookResult> HandleAsync(WebhookRequest request, CancellationToken cancellationToken);
}
