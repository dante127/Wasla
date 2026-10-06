using Wasla.BuildingBlocks.Domain;

namespace Wasla.Messages.Domain;

public enum OutboxMessageStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    DeadLettered = 3,
}

/// <summary>
/// Transactional outbox row for provider-bound work (docs/events.md, ADR-0004).
/// Written in the same SaveChanges as the message it dispatches; processed by workers
/// outside the HTTP request path.
/// </summary>
public sealed class OutboxMessage : ITenantOwned
{
    private OutboxMessage()
    {
        Kind = string.Empty;
        Payload = string.Empty;
    }

    private OutboxMessage(TenantId tenantId, string kind, string payload, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Kind = kind;
        Payload = payload;
        Status = OutboxMessageStatus.Pending;
        NextAttemptAt = now;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public string Kind { get; private set; }

    public string Payload { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? LastError { get; private set; }

    public const string MessageQueuedForSendKind = "message.queued_for_send";

    public static OutboxMessage Create(TenantId tenantId, string kind, string payload, DateTimeOffset now) =>
        new(tenantId, kind, payload, now);

    public void MarkProcessed(DateTimeOffset now)
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = now;
        LastError = null;
    }

    public void Reschedule(DateTimeOffset now, TimeSpan delay, string? error)
    {
        Status = OutboxMessageStatus.Pending;
        NextAttemptAt = now + delay;
        LastError = error;
    }

    public void MarkDeadLettered(DateTimeOffset now, string error)
    {
        Status = OutboxMessageStatus.DeadLettered;
        ProcessedAt = now;
        LastError = error;
    }
}
