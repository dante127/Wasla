using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Infrastructure.Configurations;

public sealed class InboxEventConfiguration : IEntityTypeConfiguration<InboxEvent>
{
    public void Configure(EntityTypeBuilder<InboxEvent> builder)
    {
        builder.ToTable("InboxEvents", "channels");

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(entity => entity.Provider).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.ExternalEventId).HasMaxLength(300);

        // Raw payload is text (not jsonb): signed-but-unparseable bodies must survive
        // for forensics (docs/webhooks.md §2).
        builder.Property(entity => entity.Payload).HasColumnType("text").IsRequired();
        builder.Property(entity => entity.BodyHash).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.HeadersFingerprint).HasMaxLength(64);
        builder.Property(entity => entity.SignatureValid).IsRequired();
        builder.Property(entity => entity.ReceivedAt).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entity => entity.LastError).HasMaxLength(1000);

        // Event-level dedupe: the same provider delivery never inserts twice.
        builder.HasIndex(entity => new { entity.ChannelId, entity.BodyHash }).IsUnique();

        // Claim query support.
        builder.HasIndex(entity => new { entity.Status, entity.NextAttemptAt });
    }
}

public sealed class ChannelCredentialConfiguration : IEntityTypeConfiguration<ChannelCredential>
{
    public void Configure(EntityTypeBuilder<ChannelCredential> builder)
    {
        builder.ToTable("ChannelCredentials", "channels");

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(entity => entity.ChannelId).HasConversion(id => id.Value, value => new ChannelId(value));
        builder.Property(entity => entity.Key).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.ProtectedValue).HasColumnType("text").IsRequired();
        builder.Property(entity => entity.UpdatedAt).IsRequired();

        builder.HasIndex(entity => new { entity.ChannelId, entity.Key }).IsUnique();
    }
}
