using Wasla.BuildingBlocks.Domain;

namespace Wasla.Identity.Domain;

/// <summary>
/// A rotating refresh token. Only a hash of the token is stored; tokens are single-use
/// (rotation) and reuse of a rotated token revokes the entire chain.
/// </summary>
public sealed class RefreshToken : AggregateRoot<RefreshTokenId>, ITenantOwned
{
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    private RefreshToken(
        RefreshTokenId id,
        TenantId tenantId,
        UserId userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        RefreshTokenId? rotatedFromId)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = now;
        RotatedFromId = rotatedFromId;
    }

    public TenantId TenantId { get; private set; }

    public UserId UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public RefreshTokenId? RotatedFromId { get; private set; }

    public static RefreshToken Issue(
        TenantId tenantId,
        UserId userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        RefreshTokenId? rotatedFromId = null) =>
        new(RefreshTokenId.New(), tenantId, userId, tokenHash, expiresAt, now, rotatedFromId);

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
