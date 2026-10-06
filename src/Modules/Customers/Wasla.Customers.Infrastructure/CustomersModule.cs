using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Wasla.Customers.Application;
using Wasla.Customers.Application.Abstractions;
using Wasla.Customers.Application.Services;

namespace Wasla.Customers.Infrastructure;

/// <summary>Customers module: service registration and endpoint mapping.</summary>
public sealed class CustomersModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CustomersDbContext>((serviceProvider, options) => options
            .UseNpgsql(
                serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "customers")));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomersUnitOfWork, CustomersUnitOfWork>();
        services.AddScoped<CustomerIdentityResolver>();

        services.AddScoped<SearchCustomersHandler>();
        services.AddScoped<GetCustomerHandler>();
        services.AddScoped<CreateCustomerHandler>();
        services.AddScoped<UpdateCustomerHandler>();
        services.AddScoped<AddIdentityHandler>();
        services.AddScoped<AddNoteHandler>();
        services.AddScoped<AddTagsHandler>();
        services.AddScoped<RemoveTagHandler>();
        services.AddScoped<GetTimelineHandler>();

        services.AddScoped<IValidator<CreateCustomerRequest>, CreateCustomerValidator>();
        services.AddScoped<IValidator<AddIdentityRequest>, AddIdentityValidator>();
        services.AddScoped<IValidator<AddNoteRequest>, AddNoteValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/customers");
    }
}

internal sealed class CustomersUnitOfWork(CustomersDbContext dbContext) : ICustomersUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
