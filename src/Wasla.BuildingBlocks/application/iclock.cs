namespace Wasla.BuildingBlocks.Application;

/// <summary>Abstracts the system clock so time is controllable and testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
