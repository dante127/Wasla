using System.Text;

namespace Wasla.BuildingBlocks.Application.Paging;

/// <summary>
/// Opaque cursor codec for keyset pagination: encodes (timestamp ticks, id) as base64.
/// </summary>
public static class CursorCodec
{
    public static string Encode(DateTimeOffset timestamp, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{timestamp.UtcTicks}|{id:N}"));

    public static bool TryDecode(string? cursor, out DateTimeOffset timestamp, out Guid id)
    {
        timestamp = default;
        id = default;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');

            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks))
            {
                return false;
            }

            if (!Guid.TryParseExact(parts[1], "N", out id))
            {
                return false;
            }

            timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
