using Wasla.BuildingBlocks.Domain;

namespace Wasla.BuildingBlocks.Application.Contracts;

/// <summary>Event names used on the realtime hub (client-facing contract constants).</summary>
public static class RealtimeEvents
{
    public const string NewMessage = "NewMessage";

    public const string MessageStatusChanged = "MessageStatusChanged";

    public const string ConversationUpdated = "ConversationUpdated";

    public const string ConversationAssigned = "ConversationAssigned";

    public const string ConversationTagged = "ConversationTagged";

    public const string Typing = "Typing";

    public const string PresenceChanged = "PresenceChanged";
}

// Payloads are deliberately small (ids + minimal fields — docs/roadmap.md §3.3).

public sealed record NewMessageEvent(
    Guid ConversationId,
    Guid MessageId,
    string Direction,
    string? Preview,
    DateTimeOffset OccurredAt);

public sealed record MessageStatusChangedEvent(
    Guid ConversationId,
    Guid MessageId,
    string Status,
    string? ProviderMessageId,
    DateTimeOffset OccurredAt);

public sealed record ConversationUpdatedEvent(
    Guid ConversationId,
    string Change,
    string? Value,
    DateTimeOffset OccurredAt);

public sealed record ConversationAssignedEvent(
    Guid ConversationId,
    Guid? AssignedUserId,
    Guid? AssignedTeamId,
    DateTimeOffset OccurredAt);

public sealed record ConversationTaggedEvent(
    Guid ConversationId,
    Guid TagId,
    bool Added,
    DateTimeOffset OccurredAt);

public sealed record TypingEvent(Guid ConversationId, Guid UserId, DateTimeOffset OccurredAt);

public sealed record PresenceChangedEvent(Guid UserId, bool Online, DateTimeOffset OccurredAt);

/// <summary>
/// Realtime fan-out to a tenant's connected agents. Implementations must never throw
/// into business flows — failed fan-out is logged, not surfaced (ADR-0006).
/// </summary>
public interface IRealtimePublisher
{
    Task PublishToTenantAsync(
        TenantId tenantId,
        string eventName,
        object payload,
        CancellationToken cancellationToken);
}
