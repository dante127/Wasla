using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Contracts;
using Wasla.BuildingBlocks.Domain;
using Wasla.Analytics.Application.Abstractions;

namespace Wasla.Analytics.Application;

internal static class ReportMath
{
    internal static (DateTimeOffset From, DateTimeOffset To) Normalize(DateTimeOffset? from, DateTimeOffset? to, IClock clock)
    {
        var rangeTo = to ?? clock.UtcNow;
        var rangeFrom = from ?? rangeTo.AddDays(-30);

        return rangeFrom <= rangeTo ? (rangeFrom, rangeTo) : (rangeTo, rangeFrom);
    }

    internal static bool InRange(DateTimeOffset value, DateTimeOffset from, DateTimeOffset to) =>
        value >= from && value <= to;

    internal static double? AverageMinutes(IEnumerable<double> values)
    {
        var list = values.ToList();

        return list.Count == 0 ? null : Math.Round(list.Average(), 1);
    }

    internal static double? DurationMinutes(DateTimeOffset? end, DateTimeOffset start) =>
        end is null ? null : (end.Value - start).TotalMinutes;
}

/// <summary>Overview report: volume + first-response/resolution metrics for a date range.</summary>
public sealed class GetAnalyticsOverviewHandler(
    ITenantContext tenantContext,
    IAnalyticsRepository repository,
    IClock clock)
{
    public async Task<Result<OverviewReport>> HandleAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<OverviewReport>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var (rangeFrom, rangeTo) = ReportMath.Normalize(from, to, clock);
        var facts = await repository.ListFactsAsync(tenantId, tracking: false, cancellationToken);
        var rollups = await repository.ListRollupsAsync(tenantId, tracking: false, cancellationToken);

        var opened = facts.Count(fact => ReportMath.InRange(fact.OpenedAt, rangeFrom, rangeTo));
        var resolved = facts.Count(fact => fact.ResolvedAt is { } resolvedAt && ReportMath.InRange(resolvedAt, rangeFrom, rangeTo));

        var firstResponse = ReportMath.AverageMinutes(
            facts
                .Where(fact => fact.FirstResponseAt is { } first && ReportMath.InRange(first, rangeFrom, rangeTo))
                .Select(fact => ReportMath.DurationMinutes(fact.FirstResponseAt, fact.OpenedAt)!.Value));

        var resolution = ReportMath.AverageMinutes(
            facts
                .Where(fact => fact.ResolvedAt is { } resolvedAt && ReportMath.InRange(resolvedAt, rangeFrom, rangeTo))
                .Select(fact => ReportMath.DurationMinutes(fact.ResolvedAt, fact.OpenedAt)!.Value));

        var fromDate = DateOnly.FromDateTime(rangeFrom.UtcDateTime);
        var toDate = DateOnly.FromDateTime(rangeTo.UtcDateTime);

        var daily = rollups
            .Where(rollup => rollup.Date >= fromDate && rollup.Date <= toDate)
            .OrderBy(rollup => rollup.Date)
            .Select(rollup => new DailyPoint(
                rollup.Date,
                rollup.ConversationsOpened,
                rollup.ConversationsResolved,
                rollup.MessagesInbound,
                rollup.MessagesOutbound))
            .ToList();

        return Result.Success(new OverviewReport(
            opened,
            resolved,
            firstResponse,
            resolution,
            daily.Sum(day => day.MessagesInbound),
            daily.Sum(day => day.MessagesOutbound),
            daily));
    }
}

/// <summary>Per-agent performance report, attributed by conversation assignment at recompute time.</summary>
public sealed class GetAnalyticsAgentReportHandler(
    ITenantContext tenantContext,
    IAnalyticsRepository repository,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<AgentReportItem>>> HandleAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<AgentReportItem>>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var (rangeFrom, rangeTo) = ReportMath.Normalize(from, to, clock);
        var facts = await repository.ListFactsAsync(tenantId, tracking: false, cancellationToken);

        var items = facts
            .Where(fact => fact.AssignedUserId is not null)
            .GroupBy(fact => fact.AssignedUserId!.Value)
            .Select(group => new AgentReportItem(
                group.Key,
                group.Count(fact => ReportMath.InRange(fact.OpenedAt, rangeFrom, rangeTo)),
                group.Count(fact => fact.ResolvedAt is { } resolvedAt && ReportMath.InRange(resolvedAt, rangeFrom, rangeTo)),
                ReportMath.AverageMinutes(
                    group
                        .Where(fact => fact.FirstResponseAt is { } first && ReportMath.InRange(first, rangeFrom, rangeTo))
                        .Select(fact => ReportMath.DurationMinutes(fact.FirstResponseAt, fact.OpenedAt)!.Value)),
                ReportMath.AverageMinutes(
                    group
                        .Where(fact => fact.ResolvedAt is { } resolvedAt && ReportMath.InRange(resolvedAt, rangeFrom, rangeTo))
                        .Select(fact => ReportMath.DurationMinutes(fact.ResolvedAt, fact.OpenedAt)!.Value))))
            .OrderByDescending(item => item.AssignedConversations)
            .ToList();

        return Result.Success<IReadOnlyList<AgentReportItem>>(items);
    }
}

/// <summary>Per-channel volume report; message counts are attributed via their conversation's channel.</summary>
public sealed class GetAnalyticsChannelReportHandler(
    ITenantContext tenantContext,
    IAnalyticsRepository repository,
    IChannelInfoProvider channels,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<ChannelReportItem>>> HandleAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<ChannelReportItem>>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var (rangeFrom, rangeTo) = ReportMath.Normalize(from, to, clock);
        var facts = await repository.ListFactsAsync(tenantId, tracking: false, cancellationToken);

        var grouped = facts
            .GroupBy(fact => fact.ChannelId)
            .ToList();

        var channelLookup = await channels.GetManyAsync(
            tenantId,
            grouped.Select(group => group.Key).ToList(),
            cancellationToken);

        var items = grouped
            .Select(group => new ChannelReportItem(
                group.Key,
                channelLookup.TryGetValue(group.Key, out var info) ? info.DisplayName : group.Key.ToString(),
                group.Count(fact => ReportMath.InRange(fact.OpenedAt, rangeFrom, rangeTo)),
                group.Count(fact => fact.ResolvedAt is { } resolvedAt && ReportMath.InRange(resolvedAt, rangeFrom, rangeTo)),
                group.Where(fact => ReportMath.InRange(fact.OpenedAt, rangeFrom, rangeTo)).Sum(fact => fact.MessageCountInbound),
                group.Where(fact => ReportMath.InRange(fact.OpenedAt, rangeFrom, rangeTo)).Sum(fact => fact.MessageCountOutbound)))
            .OrderByDescending(item => item.ConversationsOpened)
            .ToList();

        return Result.Success<IReadOnlyList<ChannelReportItem>>(items);
    }
}
