using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Infrastructure.Configurations;

public sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.ToTable("MediaFiles", "messages");

        builder.HasKey(media => media.Id);
        builder.Property(media => media.Id).HasConversion(id => id.Value, value => new MediaFileId(value));
        builder.Property(media => media.TenantId).HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(media => media.StorageKey).IsRequired().HasMaxLength(500);
        builder.Property(media => media.FileName).IsRequired().HasMaxLength(300);
        builder.Property(media => media.ContentType).IsRequired().HasMaxLength(150);
        builder.Property(media => media.Size).IsRequired();
        builder.Property(media => media.Hash).IsRequired().HasMaxLength(64);
        builder.Property(media => media.ScanStatus).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(media => media.CreatedAt).IsRequired();

        builder.HasIndex(media => media.StorageKey).IsUnique();
    }
}
