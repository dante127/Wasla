using Wasla.BuildingBlocks.Web.Permissions;
using Wasla.Identity.Domain;
using Wasla.Messages.Application;

namespace Wasla.Api.Endpoints;

public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/media");

        group.MapPost("/", async (
            HttpRequest request,
            UploadMediaHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["A multipart form with a file is required."],
                });
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["A non-empty file is required."],
                });
            }

            await using var stream = file.OpenReadStream();
            var result = await handler.HandleAsync(file.FileName, file.ContentType, file.Length, stream, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : ApiResults.Problem(httpContext, result.Error);
        })
        .RequireAuthorization(PermissionPolicies.For(PermissionCatalog.Messages.Send))
        .DisableAntiforgery();
    }
}
