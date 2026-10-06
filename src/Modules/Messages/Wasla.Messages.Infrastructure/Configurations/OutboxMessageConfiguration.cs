using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Infrastructure.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "messages");

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(entity => entity.Kind).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Payload).HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.Attempts).IsRequired();
        builder.Property(entity => entity.NextAttemptAt).IsRequired();
        builder.Property(entity => entity.CreatedAt).IsRequired();
        builder.Property(entity => entity.LastError).HasMaxLength(1000);

        // Claim query support.
        builder.HasIndex(entity => new { entity.Status, entity.NextAttemptAt });
    }
}
