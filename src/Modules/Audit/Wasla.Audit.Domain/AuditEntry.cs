using Wasla.BuildingBlocks.Domain;

namespace Wasla.Audit.Domain;

public readonly record struct AuditEntryId(Guid Value)
{
    public static AuditEntryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

/// <summary>
/// An append-only audit log entry. Not tenant-owned: entries may exist without a tenant
/// context (e.g. failed logins), so it is exempt from tenant query filters and the write
/// guard; immutability is enforced by having no update/delete paths.
/// </summary>
public sealed class AuditEntry : AggregateRoot<AuditEntryId>
{
    private AuditEntry()
    {
        Action = string.Empty;
        EntityType = string.Empty;
    }

    private AuditEntry(
        AuditEntryId id,
        TenantId? tenantId,
        UserId? actorUserId,
        string action,
        string entityType,
        string? entityId,
        string? metadata,
        DateTimeOffset occurredAt)
        : base(id)
    {
        TenantId = tenantId;
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Metadata = metadata;
        OccurredAt = occurredAt;
    }

    public TenantId? TenantId { get; private set; }

    public UserId? ActorUserId { get; private set; }

    public string Action { get; private set; }

    public string EntityType { get; private set; }

    public string? EntityId { get; private set; }

    public string? Metadata { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditEntry Record(
        TenantId? tenantId,
        UserId? actorUserId,
        string action,
        string entityType,
        string? entityId,
        string? metadata,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Audit action is required.", nameof(action));
        }

        return new AuditEntry(
            AuditEntryId.New(),
            tenantId,
            actorUserId,
            action.Trim(),
            entityType.Trim(),
            entityId,
            metadata,
            occurredAt);
    }
}
