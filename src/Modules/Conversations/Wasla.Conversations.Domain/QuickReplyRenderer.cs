using System.Text.RegularExpressions;

namespace Wasla.Conversations.Domain;

/// <summary>
/// Safe quick-reply template renderer: only <c>{{variable}}</c> placeholders from a closed
/// allowlist are supported. There is no expression evaluation and no code execution.
/// </summary>
public static class QuickReplyRenderer
{
    public static readonly IReadOnlySet<string> AllowedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "customer.name",
        "customer.phone",
        "customer.email",
        "agent.name",
        "tenant.name",
    };

    private static readonly Regex PlaceholderRegex = new(
        @"\{\{\s*([a-zA-Z0-9_.]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Throws <see cref="ArgumentException"/> when the template uses unknown variables.</summary>
    public static void ValidateTemplate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        foreach (Match match in PlaceholderRegex.Matches(template))
        {
            var name = match.Groups[1].Value;

            if (!AllowedVariables.Contains(name))
            {
                throw new ArgumentException($"Unknown template variable '{name}'.", nameof(template));
            }
        }
    }

    /// <summary>Renders the template; missing values become empty strings.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string?> values)
    {
        ValidateTemplate(template);
        ArgumentNullException.ThrowIfNull(values);

        var lookup = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);

        return PlaceholderRegex.Replace(template, match =>
            lookup.TryGetValue(match.Groups[1].Value, out var value) ? value ?? string.Empty : string.Empty);
    }
}
