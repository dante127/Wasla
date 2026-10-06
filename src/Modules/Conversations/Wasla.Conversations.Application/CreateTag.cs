using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application;

/// <summary>Creates a tag in the current tenant's catalog.</summary>
public sealed class CreateTagHandler(
    ITenantContext tenantContext,
    ITagRepository tags,
    IConversationsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<TagListItem>> HandleAsync(CreateTagRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<TagListItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var key = (request.Key ?? string.Empty).Trim().ToLowerInvariant();

        if (await tags.KeyExistsAsync(tenantId, key, cancellationToken))
        {
            return Result.Failure<TagListItem>(
                new Error("tags.key_conflict", "A tag with this key already exists."));
        }

        Tag tag;

        try
        {
            tag = Tag.Create(tenantId, key, request.Name, request.Color, clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<TagListItem>(new Error("tags.invalid", exception.Message));
        }

        await tags.AddAsync(tag, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TagListItem(tag.Id.Value, tag.Key, tag.Name, tag.Color));
    }
}
