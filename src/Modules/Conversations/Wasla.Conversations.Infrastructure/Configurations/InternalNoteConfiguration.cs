using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Infrastructure.Configurations;

public sealed class InternalNoteConfiguration : IEntityTypeConfiguration<InternalNote>
{
    public void Configure(EntityTypeBuilder<InternalNote> builder)
    {
        builder.ToTable("InternalNotes", "conversations");

        builder.HasKey(note => note.Id);
        builder.Property(note => note.Id)
            .HasConversion(id => id.Value, value => new InternalNoteId(value));
        builder.Property(note => note.ConversationId)
            .HasConversion(id => id.Value, value => new ConversationId(value));
        builder.Property(note => note.Body).IsRequired().HasMaxLength(4000);
        builder.Property(note => note.CreatedAt).IsRequired();

        builder.HasMany(note => note.Mentions)
            .WithOne()
            .HasForeignKey(mention => mention.NoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NoteMentionConfiguration : IEntityTypeConfiguration<NoteMention>
{
    public void Configure(EntityTypeBuilder<NoteMention> builder)
    {
        builder.ToTable("NoteMentions", "conversations");

        builder.HasKey(mention => new { mention.NoteId, mention.MentionType, mention.TargetId });
        builder.Property(mention => mention.NoteId)
            .HasConversion(id => id.Value, value => new InternalNoteId(value));
        builder.Property(mention => mention.MentionType).HasConversion<string>().IsRequired().HasMaxLength(10);
    }
}
