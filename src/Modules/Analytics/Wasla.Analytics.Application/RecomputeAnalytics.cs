using Wasla.BuildingBlocks.Application;
using Wasla.Analytics.Application.Abstractions;
using Wasla.Analytics.Domain;
using Wasla.Conversations.Application.Contracts;
using Wasla.Messages.Application.Contracts;

namespace Wasla.Analytics.Application;

/// <summary>
/// Idempotent recompute: upserts per-conversation facts (keyed by tenant + conversation)
/// and fully rebuilds daily rollups from the fact + message sources. Running it twice
/// yields identical state.
/// </summary>
public sealed class AnalyticsRecomputeService(
    ITenantContext tenantContext,
    IConversationAnalyticsSource conversations,
    IMessageAnalyticsSource messages,
    IAnalyticsRepository repository,
    IAnalyticsUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<int> RecomputeAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            throw new InvalidOperationException("Analytics recompute requires a resolved tenant context.");
        }

        var records = await conversations.ListAsync(tenantId, cancellationToken);
        var counts = await messages.GetConversationCountsAsync(
            tenantId,
            records.Select(record => record.ConversationId).ToList(),
            cancellationToken);

        var existingFacts = await repository.ListFactsAsync(tenantId, tracking: true, cancellationToken);
        var factsByConversation = existingFacts.ToDictionary(fact => fact.ConversationId);

        foreach (var record in records)
        {
            var messageCounts = counts.GetValueOrDefault(record.ConversationId);
            var inbound = messageCounts?.Inbound ?? 0;
            var outbound = messageCounts?.Outbound ?? 0;

            if (factsByConversation.TryGetValue(record.ConversationId, out var existing))
            {
                existing.Update(
                    record.ChannelId,
                    record.CustomerId,
                    record.AssignedUserId,
                    record.AssignedTeamId,
                    record.Status,
                    record.OpenedAt,
                    record.LastMessageAt,
                    record.FirstResponseAt,
                    record.ResolvedAt,
                    record.UpdatedAt,
                    inbound,
                    outbound);
            }
            else
            {
                var fact = FactConversation.Create(record.ConversationId, tenantId);

                fact.Update(
                    record.ChannelId,
                    record.CustomerId,
                    record.AssignedUserId,
                    record.AssignedTeamId,
                    record.Status,
                    record.OpenedAt,
                    record.LastMessageAt,
                    record.FirstResponseAt,
                    record.ResolvedAt,
                    record.UpdatedAt,
                    inbound,
                    outbound);

                await repository.AddFactAsync(fact, cancellationToken);
            }
        }

        // Rollups: delete + insert per tenant = idempotent rebuild.
        var existingRollups = await repository.ListRollupsAsync(tenantId, tracking: true, cancellationToken);
        repository.RemoveRollups(existingRollups);

        var openedByDate = records
            .GroupBy(record => DateOnly.FromDateTime(record.OpenedAt.UtcDateTime))
            .ToDictionary(group => group.Key, group => group.Count());

        var resolvedByDate = records
            .Where(record => record.ResolvedAt is not null)
            .GroupBy(record => DateOnly.FromDateTime(record.ResolvedAt!.Value.UtcDateTime))
            .ToDictionary(group => group.Key, group => group.Count());

        var dailyMessages = await messages.GetDailyCountsAsync(
            tenantId,
            DateTimeOffset.UnixEpoch,
            clock.UtcNow,
            cancellationToken);

        var messageByDate = dailyMessages.ToDictionary(day => day.Date);

        var allDates = openedByDate.Keys
            .Concat(resolvedByDate.Keys)
            .Concat(messageByDate.Keys)
            .Distinct()
            .OrderBy(date => date);

        foreach (var date in allDates)
        {
            var messageCounts = messageByDate.GetValueOrDefault(date);

            await repository.AddRollupAsync(
                DailyRollup.Create(
                    tenantId,
                    date,
                    openedByDate.GetValueOrDefault(date),
                    resolvedByDate.GetValueOrDefault(date),
                    messageCounts?.Inbound ?? 0,
                    messageCounts?.Outbound ?? 0),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return records.Count;
    }
}

/// <summary>Thin handler wrapper for the on-demand recompute endpoint.</summary>
public sealed class RecomputeAnalyticsHandler(AnalyticsRecomputeService service)
{
    public Task<int> HandleAsync(CancellationToken cancellationToken) =>
        service.RecomputeAsync(cancellationToken);
}
