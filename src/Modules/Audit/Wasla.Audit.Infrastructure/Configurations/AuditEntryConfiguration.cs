using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Audit.Domain;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.Audit.Infrastructure.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries", "audit");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id)
            .HasConversion(id => id.Value, value => new AuditEntryId(value));

        builder.Property(entry => entry.TenantId)
            .HasConversion(
                tenantId => tenantId.HasValue ? tenantId.Value.Value : (Guid?)null,
                value => value.HasValue ? new TenantId(value.Value) : (TenantId?)null);

        builder.Property(entry => entry.ActorUserId)
            .HasConversion(
                userId => userId.HasValue ? userId.Value.Value : (Guid?)null,
                value => value.HasValue ? new UserId(value.Value) : (UserId?)null);

        builder.Property(entry => entry.Action).IsRequired().HasMaxLength(200);
        builder.Property(entry => entry.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(entry => entry.EntityId).HasMaxLength(200);
        builder.Property(entry => entry.Metadata);
        builder.Property(entry => entry.OccurredAt).IsRequired();

        builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAt });
    }
}
