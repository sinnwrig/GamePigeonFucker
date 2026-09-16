namespace GamePigeon;

internal static class GamePigeonQueryCodec
{
    public static string DecodeDataUrl(string dataUrl)
    {
        var queryStart = dataUrl.IndexOf('?');
        var query = queryStart >= 0 ? dataUrl[(queryStart + 1)..] : dataUrl;

        string? encodedBlob = null;
        foreach (var part in query.Split('&'))
        {
            var eq = part.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            if (part[..eq] == "data")
            {
                encodedBlob = part[(eq + 1)..];
                break;
            }
        }

        if (encodedBlob is null)
        {
            throw new FormatException("GamePigeon URL is missing the data parameter");
        }

        var shuffledBlob = Uri.UnescapeDataString(encodedBlob);
        var shuffledPlain = GamePigeonCipher.Decrypt(shuffledBlob);
        return Uri.UnescapeDataString(shuffledPlain);
    }

    public static string EncodeDataUrl(string plaintextQuery, string ver = "52")
    {
        var escapedPlain = EscapeQuery(plaintextQuery, escapeValueDelimiters: false);
        var shuffledBlob = GamePigeonCipher.Encrypt(escapedPlain);
        var encodedBlob = EscapeQuery(shuffledBlob, escapeValueDelimiters: true);
        return $"data:?ver={ver}&data={encodedBlob}";
    }

    private const string QueryLiterals = "-._~!$&'()*+,;=:@/?";

    /// <summary>
    /// Mirrors NSURLComponents' percent-encoding for iMessage balloon query strings
    /// </summary>
    private static string EscapeQuery(string value, bool escapeValueDelimiters)
    {
        var literals = escapeValueDelimiters
            ? QueryLiterals.Replace("&", "").Replace("=", "")
            : QueryLiterals;
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var sb = new System.Text.StringBuilder(bytes.Length);
        foreach (var b in bytes)
        {
            var c = (char)b;
            if (b < 0x80 && (char.IsAsciiLetterOrDigit(c) || literals.Contains(c)))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%');
                sb.Append(b.ToString("X2"));
            }
        }

        return sb.ToString();
    }

    public static IReadOnlyDictionary<string, string> ParseQuery(string query)
    {
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        var fields = new Dictionary<string, string>();
        foreach (var part in trimmed.Split('&'))
        {
            if (part.Length == 0)
            {
                continue;
            }

            var eq = part.IndexOf('=');
            if (eq < 0)
            {
                fields[part] = string.Empty;
            }
            else
            {
                fields[part[..eq]] = part[(eq + 1)..];
            }
        }

        return fields;
    }

    public static string BuildQuery(IReadOnlyDictionary<string, string> fields)
    {
        return "?" + string.Join('&', fields.Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
