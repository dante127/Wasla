namespace Wasla.Analytics.Application;

public sealed record DailyPoint(
    DateOnly Date,
    int ConversationsOpened,
    int ConversationsResolved,
    int MessagesInbound,
    int MessagesOutbound);

public sealed record OverviewReport(
    int ConversationsOpened,
    int ConversationsResolved,
    double? AvgFirstResponseMinutes,
    double? AvgResolutionMinutes,
    int MessagesInbound,
    int MessagesOutbound,
    IReadOnlyList<DailyPoint> Daily);

public sealed record AgentReportItem(
    Guid UserId,
    int AssignedConversations,
    int ResolvedConversations,
    double? AvgFirstResponseMinutes,
    double? AvgResolutionMinutes);

public sealed record ChannelReportItem(
    Guid ChannelId,
    string ChannelName,
    int ConversationsOpened,
    int ConversationsResolved,
    int MessagesInbound,
    int MessagesOutbound);
