using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Infrastructure.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags", "conversations");

        builder.HasKey(tag => tag.Id);
        builder.Property(tag => tag.Id).HasConversion(id => id.Value, value => new TagId(value));
        builder.Property(tag => tag.TenantId).HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(tag => tag.Key).IsRequired().HasMaxLength(64);
        builder.Property(tag => tag.Name).IsRequired().HasMaxLength(100);
        builder.Property(tag => tag.Color).HasMaxLength(7);
        builder.Property(tag => tag.CreatedAt).IsRequired();

        builder.HasIndex(tag => new { tag.TenantId, tag.Key }).IsUnique();
    }
}
