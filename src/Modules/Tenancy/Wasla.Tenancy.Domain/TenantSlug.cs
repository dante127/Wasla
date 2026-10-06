using System.Text.RegularExpressions;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.Tenancy.Domain;

/// <summary>URL-safe unique tenant slug (lowercase letters, digits and dashes).</summary>
public sealed partial class TenantSlug : ValueObject
{
    private static readonly Regex Format = SlugRegex();

    private TenantSlug(string value) => Value = value;

    public string Value { get; }

    public static TenantSlug Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Slug is required.", nameof(value));
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (!Format.IsMatch(normalized))
        {
            throw new ArgumentException(
                "Slug must be 3-64 characters: lowercase letters, digits and dashes (no leading/trailing dash).",
                nameof(value));
        }

        return new TenantSlug(normalized);
    }

    /// <summary>Creates a slug from a trusted persistence value without validation. For EF conversions only.</summary>
    public static TenantSlug FromTrusted(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,62}[a-z0-9])$")]
    private static partial Regex SlugRegex();
}
