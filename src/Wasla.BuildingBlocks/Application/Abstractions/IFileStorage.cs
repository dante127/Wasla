namespace Wasla.BuildingBlocks.Application.Abstractions;

/// <summary>Port for binary media storage (S3-compatible in production; local disk in dev).</summary>
public interface IFileStorage
{
    Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
