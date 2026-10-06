using Wasla.BuildingBlocks.Application;
using Wasla.Teams.Application.Abstractions;
using Wasla.Teams.Domain;

namespace Wasla.Teams.Application;

/// <summary>Creates a team in the current tenant.</summary>
public sealed class CreateTeamHandler(
    ITenantContext tenantContext,
    ITeamRepository teams,
    ITeamsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<TeamListItem>> HandleAsync(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<TeamListItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        if (await teams.NameExistsAsync(tenantId, request.Name.Trim(), cancellationToken))
        {
            return Result.Failure<TeamListItem>(
                new Error("teams.name_conflict", "A team with this name already exists."));
        }

        var team = Team.Create(tenantId, request.Name, request.Description, clock.UtcNow);

        await teams.AddAsync(team, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ListTeamsHandler.ToListItem(team));
    }
}
