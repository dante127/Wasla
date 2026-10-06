using Wasla.Api.Validation;
using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Conversations.Application;
using Wasla.Identity.Domain;

namespace Wasla.Api.Endpoints;

public static class QuickReplyEndpoints
{
    public static void MapQuickReplyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quick-replies");

        group.MapGet("/", async (
            ListQuickRepliesHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.QuickReplies.Read));

        group.MapPost("/", async (
            CreateQuickReplyRequest request,
            CreateQuickReplyHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.QuickReplies.Manage))
        .AddEndpointFilter<ValidationFilter<CreateQuickReplyRequest>>();

        group.MapPost("/{quickReplyId:guid}/render", async (
            Guid quickReplyId,
            RenderQuickReplyRequest request,
            RenderQuickReplyHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(quickReplyId, request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.QuickReplies.Read));
    }
}
