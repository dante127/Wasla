using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Teams.Application.Abstractions;
using Wasla.Teams.Domain;

namespace Wasla.Teams.Infrastructure;

public sealed class TeamRepository(TeamsDbContext dbContext) : ITeamRepository
{
    public async Task AddAsync(Team team, CancellationToken cancellationToken) =>
        await dbContext.Teams.AddAsync(team, cancellationToken);

    public Task<Team?> GetByIdAsync(TenantId tenantId, TeamId teamId, CancellationToken cancellationToken) =>
        dbContext.Teams
            .Include(team => team.Members)
            .FirstOrDefaultAsync(team => team.Id == teamId && team.TenantId == tenantId, cancellationToken);

    public Task<bool> NameExistsAsync(TenantId tenantId, string name, CancellationToken cancellationToken) =>
        dbContext.Teams.AnyAsync(team => team.TenantId == tenantId && team.Name == name, cancellationToken);

    public Task<List<Team>> ListAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Teams
            .Include(team => team.Members)
            .Where(team => team.TenantId == tenantId)
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken);
}
