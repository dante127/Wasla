using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.BuildingBlocks.Application;
using Wasla.Teams.Application;
using Wasla.Teams.Application.Abstractions;

namespace Wasla.Teams.Infrastructure;

/// <summary>Teams module: service registration and endpoint mapping.</summary>
public sealed class TeamsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetSection("Database")["ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        services.AddDbContext<TeamsDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "teams")));

        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITeamsUnitOfWork, TeamsUnitOfWork>();
        services.AddScoped<ListTeamsHandler>();
        services.AddScoped<CreateTeamHandler>();
        services.AddScoped<SetTeamMembersHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/teams");
    }
}

internal sealed class TeamsUnitOfWork(TeamsDbContext dbContext) : ITeamsUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
