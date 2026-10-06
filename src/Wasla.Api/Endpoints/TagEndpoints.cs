using Wasla.Api.Validation;
using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Conversations.Application;
using Wasla.Identity.Domain;

namespace Wasla.Api.Endpoints;

public static class TagEndpoints
{
    public static void MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tags");

        group.MapGet("/", async (
            ListTagsHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization();

        group.MapPost("/", async (
            CreateTagRequest request,
            CreateTagHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Tags.Manage))
        .AddEndpointFilter<ValidationFilter<CreateTagRequest>>();
    }
}
