using Wasla.Audit.Application;
using Wasla.Audit.Domain;
using Wasla.BuildingBlocks.Application;

namespace Wasla.Audit.Infrastructure;

public sealed class AuditWriter(AuditDbContext dbContext, IClock clock) : IAuditWriter
{
    public async Task RecordAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var entry = AuditEntry.Record(
            record.TenantId,
            record.ActorUserId,
            record.Action,
            record.EntityType,
            record.EntityId,
            record.Metadata,
            clock.UtcNow);

        await dbContext.AuditEntries.AddAsync(entry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
