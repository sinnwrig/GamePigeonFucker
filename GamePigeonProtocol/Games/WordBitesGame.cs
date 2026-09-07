namespace GamePigeon.Games;

public sealed record WordBitesState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    Guid? SessionId,
    string? GameName,
    string? Letters,
    string? Language,
    string? Level,
    string? Caption)
    : GamePigeonGameState("wordbites", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields, SessionId, GameName)
{
    public override GameTurnMode TurnMode => GameTurnMode.FreeForAll;
}

internal sealed class WordBitesGame : GamePigeonGameParserBase<WordBitesState>
{
    public override string GameKey => "wordbites";

    public override WordBitesState Parse(GamePigeonEnvelope envelope)
    {
        var fields = envelope.Fields;
        return new WordBitesState(
            fields.Get("sender"),
            fields.Get("player1"),
            fields.Get("player2"),
            fields.GetInt("num"),
            fields,
            envelope.SessionId,
            envelope.GameName,
            fields.Get("letters"),
            fields.Get("lang"),
            fields.Get("level"),
            fields.Get("caption"));
    }

    public override IReadOnlyDictionary<string, string> ToFields(WordBitesState state)
    {
        var fields = new Dictionary<string, string>(state.RawFields) { ["game"] = GameKey };
        fields.SetIfNotNull("sender", state.SessionSender);
        fields.SetIfNotNull("player1", state.Player1Id);
        fields.SetIfNotNull("player2", state.Player2Id);
        fields.SetIfNotNull("num", state.MessageNumber);
        fields.SetIfNotNull("letters", state.Letters);
        fields.SetIfNotNull("lang", state.Language);
        fields.SetIfNotNull("level", state.Level);
        fields.SetIfNotNull("caption", state.Caption);
        return fields;
    }
}
