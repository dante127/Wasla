using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Infrastructure.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", "identity");

        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id)
            .HasConversion(id => id.Value, value => new RefreshTokenId(value));
        builder.Property(token => token.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value));
        builder.Property(token => token.UserId)
            .HasConversion(id => id.Value, value => new UserId(value));
        builder.Property(token => token.RotatedFromId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new RefreshTokenId(value.Value) : (RefreshTokenId?)null);

        builder.Property(token => token.TokenHash).IsRequired().HasMaxLength(128);
        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.Property(token => token.CreatedAt).IsRequired();
        builder.Property(token => token.ExpiresAt).IsRequired();
        builder.Property(token => token.RevokedAt);

        builder.HasIndex(token => new { token.TenantId, token.UserId });
    }
}
