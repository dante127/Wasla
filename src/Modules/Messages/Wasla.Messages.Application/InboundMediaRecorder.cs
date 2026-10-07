using System.Security.Cryptography;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Domain;
using Wasla.Messages.Application.Abstractions;
using Wasla.Messages.Domain;

namespace Wasla.Messages.Application;

/// <summary>
/// Records provider-downloaded inbound media: stores the bytes via IFileStorage, creates
/// the MediaFile row and attaches it to the message (A22 pipeline).
/// </summary>
public sealed class InboundMediaRecorder(
    IMessageRepository messages,
    IMessagesUnitOfWork unitOfWork,
    IFileStorage fileStorage,
    IClock clock)
{
    public async Task<Guid?> RecordAsync(
        TenantId tenantId,
        Guid messageId,
        byte[] content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var mediaFile = MediaFile.Create(tenantId, fileName, contentType, content.Length, hash, now);

        using var stream = new MemoryStream(content);
        await fileStorage.SaveAsync(mediaFile.StorageKey, stream, cancellationToken);

        await messages.AddMediaFileAsync(mediaFile, cancellationToken);

        var message = await messages.GetByIdAsync(tenantId, messageId, cancellationToken);

        if (message is null)
        {
            return null;
        }

        message.AddAttachment(mediaFile.Id, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mediaFile.Id.Value;
    }
}
