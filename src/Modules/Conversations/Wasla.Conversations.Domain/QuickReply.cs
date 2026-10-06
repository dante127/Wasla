using System.Text.RegularExpressions;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Domain;

/// <summary>A reusable quick reply template. Templates are validated against the closed
/// variable allowlist at creation; rendering never executes code (docs §25).</summary>
public sealed partial class QuickReply : AggregateRoot<QuickReplyId>, ITenantOwned
{
    private static readonly Regex KeyFormat = KeyRegex();

    private QuickReply()
    {
        Key = string.Empty;
        Text = string.Empty;
    }

    private QuickReply(QuickReplyId id, TenantId tenantId, string key, string text, DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        Key = key;
        Text = text;
        CreatedAt = now;
    }

    public TenantId TenantId { get; private set; }

    public string Key { get; private set; }

    public string Text { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static QuickReply Create(TenantId tenantId, string key, string text, DateTimeOffset now)
    {
        var normalizedKey = (key ?? string.Empty).Trim().ToLowerInvariant();

        if (!KeyFormat.IsMatch(normalizedKey))
        {
            throw new ArgumentException(
                "Quick reply key must be 2-64 characters: lowercase letters, digits and dashes.",
                nameof(key));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Quick reply text is required.", nameof(text));
        }

        if (text.Length > 2000)
        {
            throw new ArgumentException("Quick reply text must be at most 2000 characters.", nameof(text));
        }

        QuickReplyRenderer.ValidateTemplate(text);

        return new QuickReply(QuickReplyId.New(), tenantId, normalizedKey, text.Trim(), now);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$")]
    private static partial Regex KeyRegex();
}
