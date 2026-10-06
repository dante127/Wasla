using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Infrastructure.Configuration;

namespace Wasla.Messages.Infrastructure;

/// <summary>
/// Development implementation of the storage port: files are written under the app base
/// directory (e.g. bin/.../var/media). Production uses the S3-compatible implementation.
/// </summary>
public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private string Root => Path.GetFullPath(options.Value.RootPath, AppContext.BaseDirectory);

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var fileStream = File.Create(path);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);

        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        var normalized = (storageKey ?? string.Empty).Replace('\\', '/').TrimStart('/');

        if (normalized.Length == 0 || normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid storage key.", nameof(storageKey));
        }

        var fullPath = Path.GetFullPath(Path.Combine(Root, normalized));

        if (!fullPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid storage key.", nameof(storageKey));
        }

        return fullPath;
    }
}
