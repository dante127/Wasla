using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Infrastructure.Configuration;
using Wasla.Messages.Application.Abstractions;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Application;

/// <summary>
/// Validates and stores an uploaded media file (S3-compatible storage port; local disk in
/// development). Malware scanning is an integration point handled in the hardening phase.
/// </summary>
public sealed class UploadMediaHandler(
    ITenantContext tenantContext,
    IMessageRepository messages,
    IMessagesUnitOfWork unitOfWork,
    IFileStorage fileStorage,
    IOptions<StorageOptions> storageOptions,
    IClock clock)
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
        "application/pdf",
        "text/plain",
        "audio/ogg",
        "audio/mpeg",
        "video/mp4",
    };

    public async Task<Result<UploadMediaResult>> HandleAsync(
        string fileName,
        string? contentType,
        long size,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<UploadMediaResult>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var options = storageOptions.Value;

        if (size <= 0)
        {
            return Result.Failure<UploadMediaResult>(new Error("media.invalid", "The file is empty."));
        }

        if (size > options.MaxFileSizeBytes)
        {
            return Result.Failure<UploadMediaResult>(
                new Error("media.too_large", $"Files must be at most {options.MaxFileSizeBytes / 1024 / 1024} MB."));
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
        {
            return Result.Failure<UploadMediaResult>(
                new Error("media.invalid_type", $"Content type '{contentType}' is not allowed."));
        }

        var safeFileName = Path.GetFileName(fileName ?? string.Empty);

        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Length > 300)
        {
            return Result.Failure<UploadMediaResult>(
                new Error("media.invalid", "A valid file name is required."));
        }

        using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        var hash = Convert.ToHexString(SHA256.HashData(buffered.ToArray()));
        buffered.Position = 0;

        var mediaFile = MediaFile.Create(tenantId, safeFileName, contentType, size, hash, clock.UtcNow);
        await fileStorage.SaveAsync(mediaFile.StorageKey, buffered, cancellationToken);

        await messages.AddMediaFileAsync(mediaFile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UploadMediaResult(
            mediaFile.Id.Value,
            mediaFile.FileName,
            mediaFile.ContentType,
            mediaFile.Size,
            mediaFile.Hash));
    }
}
