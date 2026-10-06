using Wasla.BuildingBlocks.Domain;
using Wasla.Teams.Domain;

namespace Wasla.Teams.Application.Abstractions;

public interface ITeamRepository
{
    Task AddAsync(Team team, CancellationToken cancellationToken);

    Task<Team?> GetByIdAsync(TenantId tenantId, TeamId teamId, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(TenantId tenantId, string name, CancellationToken cancellationToken);

    Task<List<Team>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);
}
