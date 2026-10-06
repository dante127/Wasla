using Wasla.BuildingBlocks.Domain;

namespace Wasla.Audit.Application;

/// <summary>An audit fact to persist (append-only).</summary>
public sealed record AuditRecord(
    TenantId? TenantId,
    UserId? ActorUserId,
    string Action,
    string EntityType,
    string? EntityId,
    string? Metadata);

/// <summary>Module contract: write audit entries from any module.</summary>
public interface IAuditWriter
{
    Task RecordAsync(AuditRecord record, CancellationToken cancellationToken);
}
