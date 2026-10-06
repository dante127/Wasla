using Wasla.BuildingBlocks.Domain;

namespace Wasla.Channels.Domain;

public enum InboxEventStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Skipped = 3,
    Failed = 4,
    DeadLettered = 5,
}

/// <summary>
/// A raw provider webhook event (inbox pattern, docs/webhooks.md). Persisted fast after
/// signature verification; processed by a background worker with retries and
/// dead-lettering. Payload is stored as text so unparseable-but-signed bodies survive
/// for forensics.
/// </summary>
public sealed class InboxEvent : ITenantOwned
{
    private InboxEvent()
    {
        Payload = string.Empty;
        BodyHash = string.Empty;
    }

    private InboxEvent(
        TenantId tenantId,
        Guid channelId,
        ChannelType provider,
        string? externalEventId,
        string payload,
        string bodyHash,
        string? headersFingerprint,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ChannelId = channelId;
        Provider = provider;
        ExternalEventId = externalEventId;
        Payload = payload;
        BodyHash = bodyHash;
        HeadersFingerprint = headersFingerprint;
        SignatureValid = true;
        ReceivedAt = now;
        Status = InboxEventStatus.Pending;
        NextAttemptAt = now;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public Guid ChannelId { get; private set; }

    public ChannelType Provider { get; private set; }

    public string? ExternalEventId { get; private set; }

    public string Payload { get; private set; }

    public string BodyHash { get; private set; }

    public string? HeadersFingerprint { get; private set; }

    public bool SignatureValid { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public InboxEventStatus Status { get; private set; }

    public string? LastError { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public static InboxEvent Create(
        TenantId tenantId,
        Guid channelId,
        ChannelType provider,
        string? externalEventId,
        string payload,
        string bodyHash,
        string? headersFingerprint,
        DateTimeOffset now) =>
        new(tenantId, channelId, provider, externalEventId, payload, bodyHash, headersFingerprint, now);

    public void SetExternalEventId(string externalEventId) => ExternalEventId = externalEventId;

    public void MarkProcessed(DateTimeOffset now)
    {
        Status = InboxEventStatus.Processed;
        ProcessedAt = now;
        LastError = null;
    }

    public void MarkSkipped(DateTimeOffset now, string reason)
    {
        Status = InboxEventStatus.Skipped;
        ProcessedAt = now;
        LastError = reason;
    }

    public void MarkFailed(DateTimeOffset now, string error, DateTimeOffset nextAttemptAt)
    {
        Status = InboxEventStatus.Failed;
        LastError = error;
        NextAttemptAt = nextAttemptAt;
    }

    public void MarkPendingRetry(DateTimeOffset now, string error, DateTimeOffset nextAttemptAt)
    {
        Status = InboxEventStatus.Pending;
        LastError = error;
        NextAttemptAt = nextAttemptAt;
    }

    public void MarkDeadLettered(DateTimeOffset now, string error)
    {
        Status = InboxEventStatus.DeadLettered;
        ProcessedAt = now;
        LastError = error;
    }
}
