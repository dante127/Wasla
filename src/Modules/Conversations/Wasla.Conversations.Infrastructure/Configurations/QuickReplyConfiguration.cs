using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Infrastructure.Configurations;

public sealed class QuickReplyConfiguration : IEntityTypeConfiguration<QuickReply>
{
    public void Configure(EntityTypeBuilder<QuickReply> builder)
    {
        builder.ToTable("QuickReplies", "conversations");

        builder.HasKey(quickReply => quickReply.Id);
        builder.Property(quickReply => quickReply.Id)
            .HasConversion(id => id.Value, value => new QuickReplyId(value));
        builder.Property(quickReply => quickReply.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(quickReply => quickReply.Key).IsRequired().HasMaxLength(64);
        builder.Property(quickReply => quickReply.Text).IsRequired().HasMaxLength(2000);
        builder.Property(quickReply => quickReply.CreatedAt).IsRequired();

        builder.HasIndex(quickReply => new { quickReply.TenantId, quickReply.Key }).IsUnique();
    }
}
