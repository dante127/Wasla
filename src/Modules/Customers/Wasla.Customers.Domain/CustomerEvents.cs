using Wasla.BuildingBlocks.Domain;

namespace Wasla.Customers.Domain;

public sealed record CustomerCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    CustomerId CustomerId,
    TenantId TenantId) : IDomainEvent;

public sealed record CustomerArchived(
    Guid EventId,
    DateTimeOffset OccurredAt,
    CustomerId CustomerId,
    TenantId TenantId) : IDomainEvent;

public sealed record CustomerIdentityLinked(
    Guid EventId,
    DateTimeOffset OccurredAt,
    CustomerId CustomerId,
    TenantId TenantId,
    ChannelType ChannelType,
    string ExternalId) : IDomainEvent;

public sealed record CustomerContactAdded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    CustomerId CustomerId,
    TenantId TenantId,
    ContactType ContactType) : IDomainEvent;

public sealed record CustomerNoteAdded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    CustomerId CustomerId,
    TenantId TenantId) : IDomainEvent;

public sealed record CustomerTagAdded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    CustomerId CustomerId,
    TenantId TenantId,
    Guid TagId) : IDomainEvent;

public sealed record CustomerTagRemoved(
    Guid EventId,
    DateTimeOffset OccurredAt,
    CustomerId CustomerId,
    TenantId TenantId,
    Guid TagId) : IDomainEvent;
