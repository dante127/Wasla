using Wasla.BuildingBlocks.Domain;

namespace Wasla.BuildingBlocks.Application.ChannelAdapters;

// ---------------------------------------------------------------------------------
// Platform-level channel adapter protocol (hoisted from Wasla.Channels.Application in
// Phase 5 — see ADR-0011). The Messages module must dispatch outbound sends through a
// registry, while Channels must ingest inbound events into Messages; keeping these
// contracts here lets both modules depend on the platform instead of each other
// (the solution architecture tests forbid infrastructure-to-infrastructure and
// domain-to-domain references across modules, and bidirectional Application
// references would be a project cycle).
// ---------------------------------------------------------------------------------

/// <summary>Provider feature flags — capabilities vary by provider and evolve over time.</summary>
public sealed record ChannelCapabilities(
    bool SupportsDeliveryReceipts,
    bool SupportsReadReceipts,
    bool SupportsTyping,
    bool SupportsTemplates,
    bool SupportsMedia,
    int MaxTextLength);

/// <summary>Raw request essentials needed to verify and normalize an inbound webhook.</summary>
public sealed record ChannelWebhookContext(
    Guid ChannelId,
    TenantId TenantId,
    string HttpMethod,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Query,
    byte[] RawBody,
    DateTimeOffset ReceivedAt);

/// <summary>Webhook verification outcome. <see cref="Challenge"/> echoes GET subscription checks.</summary>
public sealed record WebhookVerificationResult(bool IsValid, string? Reason, string? Challenge)
{
    public static WebhookVerificationResult Valid(string? challenge = null) => new(true, null, challenge);

    public static WebhookVerificationResult Invalid(string reason) => new(false, reason, null);
}

/// <summary>Provider-independent inbound event produced by adapters (no domain side effects).</summary>
public abstract record NormalizedInboundEvent(DateTimeOffset OccurredAt);

/// <summary>A message received from a customer on a channel.</summary>
public sealed record InboundMessageEvent(
    string ProviderMessageId,
    string ExternalCustomerId,
    string? CustomerDisplayName,
    MessageType Type,
    string? Body,
    DateTimeOffset SentAt,
    string? ReplyToProviderMessageId,
    string? ProviderMetadata,
    IReadOnlyList<InboundMediaReference> Media) : NormalizedInboundEvent(SentAt);

/// <summary>Reference to provider-hosted media (download pipeline is post-MVP hardening).</summary>
public sealed record InboundMediaReference(
    string ProviderMediaId,
    MessageType Type,
    string? MimeType,
    string? FileName,
    string? Caption);

/// <summary>Normalized delivery-status transition for an outbound message.</summary>
public sealed record MessageStatusUpdateEvent(
    string ProviderMessageId,
    MessageStatusUpdateKind Kind,
    string? FailureReason,
    DateTimeOffset OccurredAt) : NormalizedInboundEvent(OccurredAt);

public enum MessageStatusUpdateKind
{
    Sent = 0,
    Delivered = 1,
    Read = 2,
    Failed = 3,
}

/// <summary>Provider-reported channel health signal (optional per provider).</summary>
public sealed record ChannelHealthEvent(string Detail, DateTimeOffset OccurredAt) : NormalizedInboundEvent(OccurredAt);

/// <summary>An outbound text message to be sent on a channel.</summary>
public sealed record ChannelOutboundMessage(
    TenantId TenantId,
    Guid ChannelId,
    Guid MessageId,
    string RecipientExternalId,
    string Body);

/// <summary>An outbound media message (the adapter reads bytes via IFileStorage).</summary>
public sealed record ChannelOutboundMediaMessage(
    TenantId TenantId,
    Guid ChannelId,
    Guid MessageId,
    string RecipientExternalId,
    MessageType Type,
    string StorageKey,
    string ContentType,
    string FileName,
    long Size,
    string? Caption);

public enum ChannelSendFailureKind
{
    Transient = 0,
    RateLimited = 1,
    Rejected = 2,
    Invalid = 3,
}

public sealed record ChannelSendResult(
    bool IsSuccess,
    string? ProviderMessageId,
    ChannelSendFailureKind? FailureKind,
    string? FailureReason,
    TimeSpan? RetryAfter)
{
    public static ChannelSendResult Success(string providerMessageId) =>
        new(true, providerMessageId, null, null, null);

    public static ChannelSendResult Failure(
        ChannelSendFailureKind kind,
        string reason,
        TimeSpan? retryAfter = null) =>
        new(false, null, kind, reason, retryAfter);
}

/// <summary>Result of a channel connect/credential verification.</summary>
public sealed record ChannelConnectionResult(bool IsValid, string? ExternalAccountId, string? Error)
{
    public static ChannelConnectionResult Valid(string? externalAccountId = null) => new(true, externalAccountId, null);

    public static ChannelConnectionResult Invalid(string error) => new(false, null, error);
}

/// <summary>Draft credentials being verified before they are persisted.</summary>
public sealed record ChannelCredentialDraft(IReadOnlyDictionary<string, string> Values);

/// <summary>
/// The provider abstraction. Implementations live in Wasla.Channels.Infrastructure/Adapters;
/// no provider SDK type may appear outside adapter code (docs/channels.md A10).
/// </summary>
public interface IChannelAdapter
{
    ChannelType ChannelType { get; }

    ChannelCapabilities Capabilities { get; }

    Task<WebhookVerificationResult> VerifyWebhookAsync(ChannelWebhookContext context, CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedInboundEvent>> NormalizeInboundAsync(ChannelWebhookContext context, CancellationToken cancellationToken);

    Task<ChannelSendResult> SendMessageAsync(ChannelOutboundMessage message, CancellationToken cancellationToken);

    Task<ChannelSendResult> SendMediaAsync(ChannelOutboundMediaMessage message, CancellationToken cancellationToken);
}

/// <summary>Resolves adapters by channel type; nothing outside the registry switches on provider strings.</summary>
public interface IChannelAdapterRegistry
{
    IChannelAdapter? Resolve(ChannelType channelType);
}

/// <summary>Optional adapter capability: verify credentials and register webhooks at connect time.</summary>
public interface IChannelConnectionVerifier
{
    ChannelType ChannelType { get; }

    Task<ChannelConnectionResult> VerifyAndDiscoverAsync(ChannelCredentialDraft draft, CancellationToken cancellationToken);
}

public interface IChannelConnectionVerifierRegistry
{
    IChannelConnectionVerifier? Resolve(ChannelType channelType);
}

/// <summary>Well-known credential keys stored (encrypted) per channel.</summary>
public static class ChannelCredentialKeys
{
    public const string AccessToken = "access_token";
    public const string AppSecret = "app_secret";
    public const string VerifyToken = "verify_token";
    public const string PhoneNumberId = "phone_number_id";
}

/// <summary>Minimal, provider-neutral routing facts the outbound dispatcher needs.</summary>
public sealed record ChannelRoutingInfo(ChannelType Type, string DisplayName, string Status, string? ExternalAccountId);

/// <summary>Module contract: channel routing data for outbound dispatch (implemented by Channels).</summary>
public interface IChannelRoutingProvider
{
    Task<ChannelRoutingInfo?> GetRoutingAsync(TenantId tenantId, Guid channelId, CancellationToken cancellationToken);
}
