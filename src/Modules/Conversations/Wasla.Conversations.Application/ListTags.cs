using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Abstractions;

namespace Wasla.Conversations.Application;

/// <summary>Lists the tag catalog of the current tenant.</summary>
public sealed class ListTagsHandler(
    ITenantContext tenantContext,
    ITagRepository tags)
{
    public async Task<Result<IReadOnlyList<TagListItem>>> HandleAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<TagListItem>>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var tagList = await tags.ListAsync(tenantId, cancellationToken);

        var items = tagList
            .OrderBy(tag => tag.Name, StringComparer.Ordinal)
            .Select(tag => new TagListItem(tag.Id.Value, tag.Key, tag.Name, tag.Color))
            .ToList();

        return Result.Success<IReadOnlyList<TagListItem>>(items);
    }
}
