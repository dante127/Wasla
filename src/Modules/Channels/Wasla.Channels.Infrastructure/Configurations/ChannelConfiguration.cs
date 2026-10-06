using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Channels.Domain;

namespace Wasla.Channels.Infrastructure.Configurations;

public sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.ToTable("Channels", "channels");

        builder.HasKey(channel => channel.Id);
        builder.Property(channel => channel.Id).HasConversion(id => id.Value, value => new ChannelId(value));
        builder.Property(channel => channel.TenantId).HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(channel => channel.Type).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(channel => channel.DisplayName).IsRequired().HasMaxLength(150);
        builder.Property(channel => channel.ExternalAccountId).HasMaxLength(200);
        builder.Property(channel => channel.Status).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(channel => channel.ConnectedAt).IsRequired();

        builder.HasIndex(channel => new { channel.TenantId, channel.Type });
    }
}
