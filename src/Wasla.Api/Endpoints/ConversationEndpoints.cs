using Wasla.Api.Validation;
using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Conversations.Application;
using Wasla.Conversations.Domain;
using Wasla.Identity.Domain;
using Wasla.Messages.Application;

namespace Wasla.Api.Endpoints;

public static class ConversationEndpoints
{
    public static void MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations");

        group.MapGet("/", async (
            string? cursor,
            int? limit,
            Guid? teamId,
            Guid? assignedUserId,
            string? status,
            string? priority,
            string? tagIds,
            bool? unread,
            Guid? customerId,
            Guid? channelId,
            ListConversationsHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<Guid>? parsedTagIds = null;

            if (!string.IsNullOrWhiteSpace(tagIds))
            {
                parsedTagIds = tagIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();
            }

            var query = new ConversationListQuery(
                cursor,
                limit ?? 30,
                teamId,
                assignedUserId,
                status,
                priority,
                parsedTagIds,
                unread,
                customerId,
                channelId);

            var result = await handler.HandleAsync(query, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Read));

        group.MapPost("/", async (
            CreateConversationRequest request,
            CreateConversationHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Create))
        .AddEndpointFilter<ValidationFilter<CreateConversationRequest>>();

        group.MapGet("/{conversationId:guid}", async (
            Guid conversationId,
            GetConversationHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(conversationId, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Read));

        group.MapPatch("/{conversationId:guid}", async (
            Guid conversationId,
            SetConversationPriorityRequest request,
            SetConversationPriorityHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(conversationId, request, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Update));

        group.MapGet("/{conversationId:guid}/messages", async (
            Guid conversationId,
            string? before,
            string? after,
            int? limit,
            ListMessagesHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(conversationId, before, after, limit ?? 50, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Messages.Read));

        group.MapPost("/{conversationId:guid}/messages", async (
            Guid conversationId,
            SendMessageRequest request,
            SendMessageHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                request = request with { IdempotencyKey = idempotencyKey };
            }

            var result = await handler.HandleAsync(conversationId, request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Messages.Send))
        .AddEndpointFilter<ValidationFilter<SendMessageRequest>>();

        group.MapPost("/{conversationId:guid}/assign", async (
            Guid conversationId,
            AssignConversationRequest request,
            AssignConversationHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(conversationId, request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Assign));

        group.MapPost("/{conversationId:guid}/tags", async (
            Guid conversationId,
            AddConversationTagsRequest request,
            AddConversationTagsHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(conversationId, request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Update));

        group.MapDelete("/{conversationId:guid}/tags/{tagId:guid}", async (
            Guid conversationId,
            Guid tagId,
            RemoveConversationTagHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(conversationId, tagId, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Update));

        group.MapPost("/{conversationId:guid}/notes", async (
            Guid conversationId,
            AddConversationNoteRequest request,
            AddConversationNoteHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(conversationId, request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Update));

        group.MapPost("/{conversationId:guid}/read", async (
            Guid conversationId,
            MarkConversationReadHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(conversationId, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Read));

        group.MapPost("/{conversationId:guid}/resolve", (
            Guid conversationId,
            ChangeConversationStatusHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
            ChangeStatusAsync(conversationId, ConversationStatus.Resolved, handler, httpContext, cancellationToken))
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Update));

        group.MapPost("/{conversationId:guid}/reopen", (
            Guid conversationId,
            ChangeConversationStatusHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
            ChangeStatusAsync(conversationId, ConversationStatus.Open, handler, httpContext, cancellationToken))
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Update));

        group.MapPost("/{conversationId:guid}/close", (
            Guid conversationId,
            ChangeConversationStatusHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
            ChangeStatusAsync(conversationId, ConversationStatus.Closed, handler, httpContext, cancellationToken))
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Update));

        group.MapGet("/{conversationId:guid}/timeline", async (
            Guid conversationId,
            int? page,
            int? pageSize,
            GetConversationTimelineHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(conversationId, page ?? 1, pageSize ?? 25, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Conversations.Read));
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid conversationId,
        ConversationStatus target,
        ChangeConversationStatusHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(conversationId, target, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
    }
}
