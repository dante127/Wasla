using Wasla.Api.Validation;
using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Identity.Domain;
using Wasla.Teams.Application;

namespace Wasla.Api.Endpoints;

public static class TeamEndpoints
{
    public static void MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/teams");

        group.MapGet("/", async (
            ListTeamsHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Teams.Read));

        group.MapPost("/", async (
            CreateTeamRequest request,
            CreateTeamHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Teams.Manage))
        .AddEndpointFilter<ValidationFilter<CreateTeamRequest>>();

        group.MapPut("/{teamId:guid}/members", async (
            Guid teamId,
            SetTeamMembersRequest request,
            SetTeamMembersHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(teamId, request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Teams.Manage));
    }
}
