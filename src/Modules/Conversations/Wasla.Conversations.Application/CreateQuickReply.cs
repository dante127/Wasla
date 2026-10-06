using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;

namespace Wasla.Conversations.Application;

/// <summary>Creates a quick reply (template validated against the variable allowlist).</summary>
public sealed class CreateQuickReplyHandler(
    ITenantContext tenantContext,
    IQuickReplyRepository quickReplies,
    IConversationsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<QuickReplyItem>> HandleAsync(
        CreateQuickReplyRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<QuickReplyItem>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var key = (request.Key ?? string.Empty).Trim().ToLowerInvariant();

        if (await quickReplies.KeyExistsAsync(tenantId, key, cancellationToken))
        {
            return Result.Failure<QuickReplyItem>(
                new Error("quickreplies.key_conflict", "A quick reply with this key already exists."));
        }

        QuickReply quickReply;

        try
        {
            quickReply = QuickReply.Create(tenantId, key, request.Text, clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<QuickReplyItem>(new Error("quickreplies.invalid", exception.Message));
        }

        await quickReplies.AddAsync(quickReply, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new QuickReplyItem(quickReply.Id.Value, quickReply.Key, quickReply.Text));
    }
}
