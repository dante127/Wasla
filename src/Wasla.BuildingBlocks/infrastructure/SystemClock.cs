using Wasla.BuildingBlocks.Application;

namespace Wasla.BuildingBlocks.Infrastructure;

/// <summary>Default <see cref="IClock"/> implementation using the system UTC clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
