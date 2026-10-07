using Wasla.BuildingBlocks.Domain;

namespace Wasla.Analytics.Domain;

/// <summary>
/// Denormalized per-conversation fact. Upserted idempotently (keyed by tenant +
/// conversation) by the recompute job; reports aggregate these facts.
/// </summary>
public sealed class FactConversation : ITenantOwned
{
    private FactConversation()
    {
        Status = string.Empty;
    }

    private FactConversation(Guid conversationId, TenantId tenantId)
    {
        ConversationId = conversationId;
        TenantId = tenantId;
        Status = string.Empty;
    }

    public Guid ConversationId { get; private set; }

    public TenantId TenantId { get; private set; }

    public Guid ChannelId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public Guid? AssignedTeamId { get; private set; }

    public string Status { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset? LastMessageAt { get; private set; }

    public DateTimeOffset? FirstResponseAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int MessageCountInbound { get; private set; }

    public int MessageCountOutbound { get; private set; }

    public static FactConversation Create(Guid conversationId, TenantId tenantId) =>
        new(conversationId, tenantId);

    public void Update(
        Guid channelId,
        Guid customerId,
        Guid? assignedUserId,
        Guid? assignedTeamId,
        string status,
        DateTimeOffset openedAt,
        DateTimeOffset? lastMessageAt,
        DateTimeOffset? firstResponseAt,
        DateTimeOffset? resolvedAt,
        DateTimeOffset updatedAt,
        int messageCountInbound,
        int messageCountOutbound)
    {
        ChannelId = channelId;
        CustomerId = customerId;
        AssignedUserId = assignedUserId;
        AssignedTeamId = assignedTeamId;
        Status = status;
        OpenedAt = openedAt;
        LastMessageAt = lastMessageAt;
        FirstResponseAt = firstResponseAt;
        ResolvedAt = resolvedAt;
        UpdatedAt = updatedAt;
        MessageCountInbound = messageCountInbound;
        MessageCountOutbound = messageCountOutbound;
    }
}
