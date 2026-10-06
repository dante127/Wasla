namespace Wasla.Teams.Application;

public sealed record TeamMemberItem(Guid UserId, string Role);

public sealed record TeamListItem(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<TeamMemberItem> Members);

public sealed record CreateTeamRequest(string Name, string? Description);

public sealed record SetTeamMemberItem(Guid UserId, string Role);

public sealed record SetTeamMembersRequest(IReadOnlyList<SetTeamMemberItem> Members);
