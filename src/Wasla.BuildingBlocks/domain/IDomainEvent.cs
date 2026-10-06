namespace Wasla.BuildingBlocks.Domain;

/// <summary>Marker for domain events raised by aggregates inside a module (in-process).</summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}
