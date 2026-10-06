using Wasla.BuildingBlocks.Domain;

namespace Wasla.Identity.Domain;

/// <summary>Normalized email address value object.</summary>
public sealed class EmailAddress : ValueObject
{
    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static EmailAddress Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Email is required.", nameof(value));
        }

        var normalized = value.Trim().ToLowerInvariant();

        var at = normalized.IndexOf('@');
        var isObviouslyInvalid = at <= 0
            || at == normalized.Length - 1
            || normalized.Contains(' ', StringComparison.Ordinal)
            || normalized.Length > 320;

        if (isObviouslyInvalid)
        {
            throw new ArgumentException("Email is not valid.", nameof(value));
        }

        return new EmailAddress(normalized);
    }

    /// <summary>Creates an email from a trusted persistence value without validation. For EF conversions only.</summary>
    public static EmailAddress FromTrusted(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
