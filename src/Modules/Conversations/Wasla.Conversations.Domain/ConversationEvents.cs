using Wasla.BuildingBlocks.Domain;

namespace Wasla.Conversations.Domain;

public sealed record ConversationCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    ConversationId ConversationId,
    TenantId TenantId,
    Guid CustomerId,
    Guid ChannelId) : IDomainEvent;

public sealed record ConversationAssigned(
    Guid EventId,
    DateTimeOffset OccurredAt,
    ConversationId ConversationId,
    TenantId TenantId,
    Guid? AssignedUserId,
    Guid? AssignedTeamId,
    Guid? ActorUserId) : IDomainEvent;

public sealed record ConversationStatusChanged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    ConversationId ConversationId,
    TenantId TenantId,
    ConversationStatus From,
    ConversationStatus To,
    Guid? ActorUserId) : IDomainEvent;

public sealed record ConversationTagged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    ConversationId ConversationId,
    TenantId TenantId,
    Guid TagId) : IDomainEvent;

public sealed record ConversationUntagged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    ConversationId ConversationId,
    TenantId TenantId,
    Guid TagId) : IDomainEvent;

public sealed record InternalNoteAdded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    ConversationId ConversationId,
    TenantId TenantId,
    InternalNoteId NoteId) : IDomainEvent;
