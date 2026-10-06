using Wasla.BuildingBlocks.Application;
using Wasla.Conversations.Application.Abstractions;
using Wasla.Conversations.Domain;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.Tenancy.Application.Contracts;

namespace Wasla.Conversations.Application;

/// <summary>Renders a quick reply for a specific customer using safe allow-listed variables.</summary>
public sealed class RenderQuickReplyHandler(
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IQuickReplyRepository quickReplies,
    ICustomerInfoProvider customers,
    ITenantInfoProvider tenants)
{
    public async Task<Result<RenderQuickReplyResponse>> HandleAsync(
        Guid quickReplyId,
        RenderQuickReplyRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<RenderQuickReplyResponse>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var quickReply = await quickReplies.GetByIdAsync(tenantId, new QuickReplyId(quickReplyId), cancellationToken);

        if (quickReply is null)
        {
            return Result.Failure<RenderQuickReplyResponse>(
                new Error("quickreplies.not_found", "Quick reply not found."));
        }

        var customer = await customers.GetAsync(tenantId, request.CustomerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<RenderQuickReplyResponse>(
                new Error("customers.not_found", "Customer not found."));
        }

        var tenant = await tenants.GetSummaryAsync(tenantId, cancellationToken);

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["customer.name"] = customer.DisplayName,
            ["customer.phone"] = customer.PrimaryPhone,
            ["customer.email"] = customer.PrimaryEmail,
            ["agent.name"] = currentUser.DisplayName,
            ["tenant.name"] = tenant?.Name,
        };

        return Result.Success(new RenderQuickReplyResponse(QuickReplyRenderer.Render(quickReply.Text, values)));
    }
}
