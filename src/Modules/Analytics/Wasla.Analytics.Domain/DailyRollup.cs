using Wasla.BuildingBlocks.Domain;

namespace Wasla.Analytics.Domain;

/// <summary>
/// Daily rollup row, fully rebuilt per tenant by the recompute job (delete + insert),
/// which makes recomputation idempotent by construction. Dates are UTC days.
/// </summary>
public sealed class DailyRollup : ITenantOwned
{
    private DailyRollup()
    {
    }

    private DailyRollup(TenantId tenantId, DateOnly date)
    {
        TenantId = tenantId;
        Date = date;
    }

    public TenantId TenantId { get; private set; }

    public DateOnly Date { get; private set; }

    public int ConversationsOpened { get; private set; }

    public int ConversationsResolved { get; private set; }

    public int MessagesInbound { get; private set; }

    public int MessagesOutbound { get; private set; }

    public static DailyRollup Create(
        TenantId tenantId,
        DateOnly date,
        int conversationsOpened,
        int conversationsResolved,
        int messagesInbound,
        int messagesOutbound) =>
        new(tenantId, date)
        {
            ConversationsOpened = conversationsOpened,
            ConversationsResolved = conversationsResolved,
            MessagesInbound = messagesInbound,
            MessagesOutbound = messagesOutbound,
        };
}
