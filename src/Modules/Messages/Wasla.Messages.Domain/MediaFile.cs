using Wasla.BuildingBlocks.Domain;

namespace Wasla.Messages.Domain;

/// <summary>A media object stored in S3-compatible storage (metadata only in PostgreSQL).</summary>
public sealed class MediaFile : AggregateRoot<MediaFileId>, ITenantOwned
{
    private MediaFile()
    {
        StorageKey = string.Empty;
        FileName = string.Empty;
        ContentType = string.Empty;
        Hash = string.Empty;
    }

    private MediaFile(
        MediaFileId id,
        TenantId tenantId,
        string storageKey,
        string fileName,
        string contentType,
        long size,
        string hash,
        DateTimeOffset now)
        : base(id)
    {
        TenantId = tenantId;
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        Hash = hash;
        ScanStatus = MediaScanStatus.Clean; // scanning integration point arrives with hardening
        CreatedAt = now;
    }

    public TenantId TenantId { get; private set; }

    public string StorageKey { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public long Size { get; private set; }

    public string Hash { get; private set; }

    public MediaScanStatus ScanStatus { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MediaFile Create(
        TenantId tenantId,
        string fileName,
        string contentType,
        long size,
        string hash,
        DateTimeOffset now)
    {
        var mediaFileId = MediaFileId.New();
        var storageKey = $"tenant/{tenantId.Value}/{now:yyyy}/{now:MM}/{mediaFileId.Value:N}";

        return new MediaFile(mediaFileId, tenantId, storageKey, fileName, contentType, size, hash, now);
    }
}

/// <summary>Links a media file to a message.</summary>
public sealed class Attachment : Entity<AttachmentId>
{
    private Attachment()
    {
    }

    internal Attachment(AttachmentId id, MessageId messageId, MediaFileId mediaFileId, DateTimeOffset now)
        : base(id)
    {
        MessageId = messageId;
        MediaFileId = mediaFileId;
        CreatedAt = now;
    }

    public MessageId MessageId { get; private set; }

    public MediaFileId MediaFileId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
