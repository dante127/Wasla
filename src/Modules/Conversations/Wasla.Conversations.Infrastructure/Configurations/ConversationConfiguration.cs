using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Infrastructure.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations", "conversations");

        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Id)
            .HasConversion(id => id.Value, value => new ConversationId(value));
        builder.Property(conversation => conversation.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(conversation => conversation.Status).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(conversation => conversation.Priority).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(conversation => conversation.LastMessagePreview).HasMaxLength(300);
        builder.Property(conversation => conversation.CreatedAt).IsRequired();
        builder.Property(conversation => conversation.UpdatedAt).IsRequired();

        builder.HasIndex(conversation => new { conversation.TenantId, conversation.Status, conversation.LastMessageAt });
        builder.HasIndex(conversation => new { conversation.TenantId, conversation.AssignedUserId });
        builder.HasIndex(conversation => new { conversation.TenantId, conversation.AssignedTeamId });
        builder.HasIndex(conversation => new { conversation.TenantId, conversation.CustomerId });

        builder.HasMany(conversation => conversation.Tags)
            .WithOne()
            .HasForeignKey(tag => tag.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(conversation => conversation.Notes)
            .WithOne()
            .HasForeignKey(note => note.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(conversation => conversation.Timeline)
            .WithOne()
            .HasForeignKey(entry => entry.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ConversationTagConfiguration : IEntityTypeConfiguration<ConversationTag>
{
    public void Configure(EntityTypeBuilder<ConversationTag> builder)
    {
        builder.ToTable("ConversationTags", "conversations");

        builder.HasKey(tag => new { tag.ConversationId, tag.TagId });
        builder.Property(tag => tag.ConversationId)
            .HasConversion(id => id.Value, value => new ConversationId(value));

        builder.HasIndex(tag => tag.TagId);
    }
}

public sealed class ConversationTimelineEntryConfiguration : IEntityTypeConfiguration<ConversationTimelineEntry>
{
    public void Configure(EntityTypeBuilder<ConversationTimelineEntry> builder)
    {
        builder.ToTable("ConversationTimelineEntries", "conversations");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id)
            .HasConversion(id => id.Value, value => new TimelineEntryId(value));
        builder.Property(entry => entry.ConversationId)
            .HasConversion(id => id.Value, value => new ConversationId(value));
        builder.Property(entry => entry.Type).IsRequired().HasMaxLength(80);
        builder.Property(entry => entry.Data).HasMaxLength(1000);
        builder.Property(entry => entry.OccurredAt).IsRequired();

        builder.HasIndex(entry => new { entry.ConversationId, entry.OccurredAt });
    }
}
