using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Abstractions;

namespace Wasla.Conversations.Application;

/// <summary>Lists the tenant's quick replies.</summary>
public sealed class ListQuickRepliesHandler(
    ITenantContext tenantContext,
    IQuickReplyRepository quickReplies)
{
    public async Task<Result<IReadOnlyList<QuickReplyItem>>> HandleAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<QuickReplyItem>>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var items = await quickReplies.ListAsync(tenantId, cancellationToken);

        return Result.Success<IReadOnlyList<QuickReplyItem>>(
            items.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new QuickReplyItem(item.Id.Value, item.Key, item.Text))
                .ToList());
    }
}
