namespace GamePigeon.Games;

// Classified: connect/dots = Lockstep, hunt/anagrams/wordbites = FreeForAll.
// Unclassified (default Lockstep): golf, mancala, beer, knock, fill, archery.
public enum GameTurnMode
{
    Lockstep,
    FreeForAll,
}

public enum GamePigeonPlayerSlot
{
    Unknown,
    Player1,
    Player2,
}

public abstract record GamePigeonGameState(
    string GameKey,
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    Guid? SessionId,
    string? GameName)
{
    public virtual GameTurnMode TurnMode => GameTurnMode.Lockstep;

    public virtual bool IsOpenInvite => false;

    public bool CanRespond(bool messageIsFromMe) =>
        !messageIsFromMe || (TurnMode == GameTurnMode.FreeForAll && IsOpenInvite);

    public GamePigeonPlayerSlot GetPlayerSlot(string playerUuid) => Player1Id == playerUuid
        ? GamePigeonPlayerSlot.Player1
        : Player2Id == playerUuid
            ? GamePigeonPlayerSlot.Player2
            : GamePigeonPlayerSlot.Unknown;
}

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
