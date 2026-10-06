using Wasla.Api.Validation;
using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Channels.Application;
using Wasla.Identity.Domain;

namespace Wasla.Api.Endpoints;

public static class ChannelEndpoints
{
    public static void MapChannelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/channels");

        group.MapGet("/", async (
            ListChannelsHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Channels.Read));

        group.MapPost("/", async (
            CreateChannelRequest request,
            CreateChannelHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Channels.Manage))
        .AddEndpointFilter<ValidationFilter<CreateChannelRequest>>();
    }
}
