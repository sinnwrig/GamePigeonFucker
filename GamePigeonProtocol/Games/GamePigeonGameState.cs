namespace GamePigeon.Games;

internal abstract record GamePigeonGameState(
    string GameKey,
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields);

internal static class GamePigeonFieldHelpers
{
    public static string? Get(this IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var v) ? v : null;

    public static int? GetInt(this IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : null;

    public static void SetIfNotNull(this Dictionary<string, string> fields, string key, string? value)
    {
        if (value is not null)
        {
            fields[key] = value;
        }
    }

    public static void SetIfNotNull(this Dictionary<string, string> fields, string key, int? value)
    {
        if (value is not null)
        {
            fields[key] = value.Value.ToString();
        }
    }
}
