namespace Wasla.BuildingBlocks.Infrastructure.Configuration;

/// <summary>Background worker and dispatch settings (section "Workers").</summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Workers";

    /// <summary>Whether the inbound webhook processor worker runs in this host.</summary>
    public bool InboxEnabled { get; set; } = true;

    /// <summary>Whether the outbound message dispatcher worker runs in this host.</summary>
    public bool OutboxEnabled { get; set; } = true;

    /// <summary>Idle poll interval for the workers.</summary>
    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>Items claimed per processing round.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Attempt budget before an item is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 8;

    /// <summary>Whether the analytics recompute worker runs in this host.</summary>
    public bool AnalyticsEnabled { get; set; } = true;

    /// <summary>Analytics recompute interval in minutes.</summary>
    public int AnalyticsIntervalMinutes { get; set; } = 5;
}
