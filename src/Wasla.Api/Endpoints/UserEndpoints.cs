using Wasla.Api.Validation;
using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Identity.Application.Users;
using Wasla.Identity.Domain;

namespace Wasla.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users");

        group.MapGet("/", async (
            ListUsersHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Users.Read));

        group.MapPost("/", async (
            CreateUserRequest request,
            CreateUserHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Users.Create))
        .AddEndpointFilter<ValidationFilter<CreateUserRequest>>();

        group.MapPatch("/{userId:guid}", async (
            Guid userId,
            UpdateUserRequest request,
            UpdateUserHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(userId, request, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Users.Update));

        group.MapPut("/{userId:guid}/roles", async (
            Guid userId,
            AssignRolesRequest request,
            AssignRolesHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(userId, request, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Users.Update));
    }
}
