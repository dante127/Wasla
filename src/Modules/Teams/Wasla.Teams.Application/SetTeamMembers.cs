using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Contracts;
using Wasla.Teams.Application.Abstractions;
using Wasla.Teams.Domain;

namespace Wasla.Teams.Application;

/// <summary>Replaces the member list of a team (validates that members are active tenant users).</summary>
public sealed class SetTeamMembersHandler(
    ITenantContext tenantContext,
    ITeamRepository teams,
    IMembershipVerifier membershipVerifier,
    ITeamsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<TeamListItem>> HandleAsync(
        Guid teamId,
        SetTeamMembersRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<TeamListItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var team = await teams.GetByIdAsync(tenantId, new TeamId(teamId), cancellationToken);

        if (team is null)
        {
            return Result.Failure<TeamListItem>(new Error("teams.not_found", "Team not found."));
        }

        var members = new List<(UserId UserId, TeamRole Role)>();

        foreach (var item in request.Members)
        {
            if (!Enum.TryParse<TeamRole>(item.Role, ignoreCase: true, out var role))
            {
                return Result.Failure<TeamListItem>(
                    new Error("teams.invalid_role", $"Unknown team role '{item.Role}'. Use 'Lead' or 'Member'."));
            }

            var userId = new UserId(item.UserId);

            if (!await membershipVerifier.IsActiveMemberAsync(tenantId, userId, cancellationToken))
            {
                return Result.Failure<TeamListItem>(
                    new Error("teams.member_not_found", $"User {item.UserId} is not an active member of this tenant."));
            }

            members.Add((userId, role));
        }

        try
        {
            team.ReplaceMembers(members, clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<TeamListItem>(new Error("teams.invalid_members", exception.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ListTeamsHandler.ToListItem(team));
    }
}
