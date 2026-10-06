using Wasla.Api.Validation;
using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Customers.Application;
using Wasla.Identity.Domain;

namespace Wasla.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customers");

        group.MapGet("/", async (
            string? q,
            string? tagIds,
            int? page,
            int? pageSize,
            SearchCustomersHandler handler,
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

            var result = await handler.HandleAsync(
                new CustomerSearchQuery(q, parsedTagIds, page ?? 1, pageSize ?? 25),
                cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Customers.Read));

        group.MapPost("/", async (
            CreateCustomerRequest request,
            CreateCustomerHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Customers.Update))
        .AddEndpointFilter<ValidationFilter<CreateCustomerRequest>>();

        group.MapGet("/{customerId:guid}", async (
            Guid customerId,
            GetCustomerHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(customerId, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Customers.Read));

        group.MapPatch("/{customerId:guid}", async (
            Guid customerId,
            UpdateCustomerRequest request,
            UpdateCustomerHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(customerId, request, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Customers.Update));

        group.MapGet("/{customerId:guid}/timeline", async (
            Guid customerId,
            int? page,
            int? pageSize,
            GetTimelineHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(customerId, page ?? 1, pageSize ?? 25, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Customers.Read));

        group.MapPost("/{customerId:guid}/identities", async (
            Guid customerId,
            AddIdentityRequest request,
            AddIdentityHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(customerId, request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Customers.Update))
        .AddEndpointFilter<ValidationFilter<AddIdentityRequest>>();

        group.MapPost("/{customerId:guid}/notes", async (
            Guid customerId,
            AddNoteRequest request,
            AddNoteHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(customerId, request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Customers.Update))
        .AddEndpointFilter<ValidationFilter<AddNoteRequest>>();

        group.MapPost("/{customerId:guid}/tags", async (
            Guid customerId,
            AddTagsRequest request,
            AddTagsHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(customerId, request, cancellationToken);

            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Customers.Update));

        group.MapDelete("/{customerId:guid}/tags/{tagId:guid}", async (
            Guid customerId,
            Guid tagId,
            RemoveTagHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(customerId, tagId, cancellationToken);

            return result.IsSuccess ? Results.NoContent() : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Customers.Update));
    }
}
