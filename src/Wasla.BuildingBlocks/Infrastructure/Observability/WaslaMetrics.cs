using System.Diagnostics.Metrics;

namespace Wasla.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Platform business metrics (OpenTelemetry meter "Wasla"). Counters cover the inbox
/// and outbox pipelines so alerts can key off provider/signature failures, backlog
/// growth and dead letters (docs/runbook.md alert rules).
/// </summary>
public static class WaslaMetrics
{
    public const string MeterName = "Wasla";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> InboxReceived = Meter.CreateCounter<long>("wasla.inbox.received");

    public static readonly Counter<long> InboxDuplicates = Meter.CreateCounter<long>("wasla.inbox.duplicates");

    public static readonly Counter<long> InboxProcessed = Meter.CreateCounter<long>("wasla.inbox.processed");

    public static readonly Counter<long> InboxSkipped = Meter.CreateCounter<long>("wasla.inbox.skipped");

    public static readonly Counter<long> InboxFailed = Meter.CreateCounter<long>("wasla.inbox.failed");

    public static readonly Counter<long> OutboxSent = Meter.CreateCounter<long>("wasla.outbox.sent");

    public static readonly Counter<long> OutboxRetried = Meter.CreateCounter<long>("wasla.outbox.retried");

    public static readonly Counter<long> OutboxDeadLettered = Meter.CreateCounter<long>("wasla.outbox.dead_lettered");
}
