using Wasla.BuildingBlocks.Domain;

namespace Wasla.Tenancy.Domain;

public sealed record TenantCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    TenantId TenantId,
    string Name,
    string Slug) : IDomainEvent;

public sealed record TenantSuspended(
    Guid EventId,
    DateTimeOffset OccurredAt,
    TenantId TenantId) : IDomainEvent;

public sealed record TenantReactivated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    TenantId TenantId) : IDomainEvent;
