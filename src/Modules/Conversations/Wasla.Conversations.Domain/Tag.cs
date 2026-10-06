using System.Text.RegularExpressions;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Domain;

/// <summary>
/// A tenant-scoped tag from the shared tag catalog (used for conversations and customers).
/// Tags are never hard-coded; tenants create their own catalog.
/// </summary>
public sealed partial class Tag : AggregateRoot<TagId>, ITenantOwned
{
    private static readonly Regex KeyFormat = KeyRegex();

    private Tag()
    {
        Key = string.Empty;
        Name = string.Empty;
    }

    private Tag(TagId id, TenantId tenantId, string key, string name, string? color, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        Key = key;
        Name = name;
        Color = color;
        CreatedAt = createdAt;
    }

    public TenantId TenantId { get; private set; }

    public string Key { get; private set; }

    public string Name { get; private set; }

    public string? Color { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Tag Create(TenantId tenantId, string key, string name, string? color, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tag name is required.", nameof(name));
        }

        var normalizedKey = (key ?? string.Empty).Trim().ToLowerInvariant();

        if (!KeyFormat.IsMatch(normalizedKey))
        {
            throw new ArgumentException(
                "Tag key must be 3-64 characters: lowercase letters, digits and dashes.",
                nameof(key));
        }

        var tag = new Tag(TagId.New(), tenantId, normalizedKey, name.Trim(), color?.Trim(), now);
        tag.RaiseDomainEvent(new TagCreated(Guid.NewGuid(), now, tag.Id, tenantId, tag.Key));

        return tag;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$")]
    private static partial Regex KeyRegex();
}

public sealed record TagCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    TagId TagId,
    TenantId TenantId,
    string Key) : IDomainEvent;
