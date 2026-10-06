using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Application.Auth;
using Wasla.Identity.Application.Contracts;
using Wasla.Identity.Application.Permissions;
using Wasla.Identity.Application.Roles;
using Wasla.Identity.Application.Users;
using Wasla.Identity.Infrastructure.Repositories;
using Wasla.Identity.Infrastructure.Security;

namespace Wasla.Identity.Infrastructure;

/// <summary>Identity module: service registration and endpoint mapping.</summary>
public sealed class IdentityModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
                services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SigningKey) && options.SigningKey.Length >= 32,
                "Jwt:SigningKey is required and must be at least 32 characters.")
            .ValidateOnStart();

        services.AddDbContext<IdentityDbContext>((serviceProvider, options) => options
            .UseNpgsql(
                serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IMembershipVerifier, MembershipVerifier>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPermissionResolver, RolePermissionResolver>();
        services.AddScoped<DefaultRoleSeeder>();

        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<ListUsersHandler>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<AssignRolesHandler>();
        services.AddScoped<ListRolesHandler>();

        services.AddScoped<IValidator<LoginRequest>, LoginValidator>();
        services.AddScoped<IValidator<RefreshRequest>, RefreshValidator>();
        services.AddScoped<IValidator<CreateUserRequest>, CreateUserValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/identity");
    }
}
