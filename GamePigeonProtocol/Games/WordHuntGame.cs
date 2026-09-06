namespace GamePigeon.Games;

internal sealed record WordHuntState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    string? Letters,
    string? Language,
    int? Mode,
    int? Score1,
    int? Score2,
    int? Words1,
    int? Words2,
    IReadOnlyList<string> WordsList1,
    IReadOnlyList<string> WordsList2)
    : GamePigeonGameState("hunt", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields);

internal sealed class WordHuntGame : GamePigeonGameParserBase<WordHuntState>
{
    public override string GameKey => "hunt";

    public override WordHuntState Parse(GamePigeonEnvelope envelope)
    {
        var fields = envelope.Fields;
        return new WordHuntState(
            fields.Get("sender"),
            fields.Get("player1"),
            fields.Get("player2"),
            fields.GetInt("num"),
            fields,
            fields.Get("letters"),
            fields.Get("lang"),
            fields.GetInt("mode"),
            fields.GetInt("score1"),
            fields.GetInt("score2"),
            fields.GetInt("words1"),
            fields.GetInt("words2"),
            SplitWords(fields.Get("words_list1")),
            SplitWords(fields.Get("words_list2")));
    }

    public override IReadOnlyDictionary<string, string> ToFields(WordHuntState state)
    {
        var fields = new Dictionary<string, string>(state.RawFields) { ["game"] = GameKey };
        fields.SetIfNotNull("sender", state.SessionSender);
        fields.SetIfNotNull("player1", state.Player1Id);
        fields.SetIfNotNull("player2", state.Player2Id);
        fields.SetIfNotNull("num", state.MessageNumber);
        fields.SetIfNotNull("letters", state.Letters);
        fields.SetIfNotNull("lang", state.Language);
        fields.SetIfNotNull("mode", state.Mode);
        fields.SetIfNotNull("score1", state.Score1);
        fields.SetIfNotNull("score2", state.Score2);
        fields.SetIfNotNull("words1", state.Words1);
        fields.SetIfNotNull("words2", state.Words2);
        fields["words_list1"] = string.Join('|', state.WordsList1);
        fields["words_list2"] = string.Join('|', state.WordsList2);
        return fields;
    }

    private static IReadOnlyList<string> SplitWords(string? raw) =>
        string.IsNullOrEmpty(raw) ? [] : raw.Split('|', StringSplitOptions.RemoveEmptyEntries);
}
