namespace Wasla.Identity.Application.Users;

public sealed record UserListItem(
    Guid Id,
    string DisplayName,
    string Email,
    string Status,
    IReadOnlyCollection<Guid> RoleIds,
    DateTimeOffset JoinedAt);

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    IReadOnlyList<Guid> RoleIds);

public sealed record UpdateUserRequest(string? DisplayName, string? Status);

public sealed record AssignRolesRequest(IReadOnlyList<Guid> RoleIds);
