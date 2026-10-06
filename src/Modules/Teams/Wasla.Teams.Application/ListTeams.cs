using Wasla.BuildingBlocks.Application;
using Wasla.Teams.Application.Abstractions;
using Wasla.Teams.Domain;

namespace Wasla.Teams.Application;

/// <summary>Lists the teams of the current tenant.</summary>
public sealed class ListTeamsHandler(
    ITenantContext tenantContext,
    ITeamRepository teams)
{
    public async Task<Result<IReadOnlyList<TeamListItem>>> HandleAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<TeamListItem>>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var teamList = await teams.ListAsync(tenantId, cancellationToken);

        var items = teamList
            .Select(ToListItem)
            .ToList();

        return Result.Success<IReadOnlyList<TeamListItem>>(items);
    }

    internal static TeamListItem ToListItem(Team team) =>
        new(
            team.Id.Value,
            team.Name,
            team.Description,
            team.Members
                .Select(member => new TeamMemberItem(member.UserId.Value, member.Role.ToString()))
                .ToList());
}
