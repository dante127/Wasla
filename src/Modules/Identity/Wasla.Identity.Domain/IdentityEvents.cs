using Wasla.BuildingBlocks.Domain;

namespace Wasla.Identity.Domain;

public sealed record UserRegistered(
    Guid EventId,
    DateTimeOffset OccurredAt,
    UserId UserId,
    string Email) : IDomainEvent;

public sealed record UserActivated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    UserId UserId) : IDomainEvent;

public sealed record UserDisabled(
    Guid EventId,
    DateTimeOffset OccurredAt,
    UserId UserId) : IDomainEvent;

public sealed record MembershipCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    MembershipId MembershipId,
    TenantId TenantId,
    UserId UserId) : IDomainEvent;

public sealed record MembershipRolesChanged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    MembershipId MembershipId,
    TenantId TenantId,
    IReadOnlyList<RoleId> RoleIds) : IDomainEvent;
