using System.Text.RegularExpressions;

namespace IfsaKlasik.Web.Services;

public static class RoomCodeNormalizer
{
    private static readonly Regex Valid = new("^[A-Z0-9]{4,16}$", RegexOptions.Compiled);

    public static string Normalize(string raw)
    {
        return raw.Trim().ToUpperInvariant();
    }

    public static bool IsValidFormat(string normalized)
    {
        return Valid.IsMatch(normalized);
    }

    /// <summary>Generate a readable 6-char code (no 0/O/1/I).</summary>
    public static string GenerateCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> buffer = stackalloc char[6];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }

        return new string(buffer);
    }
}
