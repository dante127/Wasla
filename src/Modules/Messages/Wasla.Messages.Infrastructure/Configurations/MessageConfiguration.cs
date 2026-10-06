using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Infrastructure.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages", "messages");

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasConversion(id => id.Value, value => new MessageId(value));
        builder.Property(message => message.TenantId).HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(message => message.Direction).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(message => message.Type).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(message => message.Body).HasMaxLength(8000);
        builder.Property(message => message.ProviderMessageId).HasMaxLength(300);
        builder.Property(message => message.ProviderMetadata);
        builder.Property(message => message.Status).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(message => message.FailureReason).HasMaxLength(500);
        builder.Property(message => message.IdempotencyKey).HasMaxLength(128);
        builder.Property(message => message.CreatedAt).IsRequired();

        // Cursor pagination key for message history.
        builder.HasIndex(message => new { message.TenantId, message.ConversationId, message.CreatedAt })
            .IsDescending(false, false, true);

        // Idempotent outbound sends.
        builder.HasIndex(message => new { message.TenantId, message.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        // Provider message id dedup for inbound (used from Phase 5).
        builder.HasIndex(message => new { message.TenantId, message.ProviderMessageId })
            .IsUnique()
            .HasFilter("\"ProviderMessageId\" IS NOT NULL");

        builder.HasMany(message => message.Attachments)
            .WithOne()
            .HasForeignKey(attachment => attachment.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments", "messages");

        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id)
            .HasConversion(id => id.Value, value => new AttachmentId(value));
        builder.Property(attachment => attachment.MessageId)
            .HasConversion(id => id.Value, value => new MessageId(value));
        builder.Property(attachment => attachment.MediaFileId)
            .HasConversion(id => id.Value, value => new MediaFileId(value));
        builder.Property(attachment => attachment.CreatedAt).IsRequired();
    }
}
