namespace Wasla.BuildingBlocks.Infrastructure.Configuration;

/// <summary>Media storage settings (section "Storage").</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Local storage root (dev implementation of IFileStorage).</summary>
    public string RootPath { get; set; } = "var/media";

    public long MaxFileSizeBytes { get; set; } = 26214400; // 25 MB
}
