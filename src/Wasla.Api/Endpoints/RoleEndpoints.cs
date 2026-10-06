using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Identity.Application.Roles;
using Wasla.Identity.Domain;

namespace Wasla.Api.Endpoints;

public static class RoleEndpoints
{
    public static void MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/roles");

        group.MapGet("/", async (
            ListRolesHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Users.Read));
    }
}
