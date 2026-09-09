namespace GamePigeon.Games;

public sealed record WordHuntState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    Guid? SessionId,
    string? GameName,
    string? Letters,
    string? Language,
    int? Mode,
    int? Score1,
    int? Score2,
    int? Words1,
    int? Words2,
    IReadOnlyList<string> WordsList1,
    IReadOnlyList<string> WordsList2,
    string? Winner)
    : GamePigeonGameState("hunt", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields, SessionId, GameName)
{
    public override GameTurnMode TurnMode => GameTurnMode.FreeForAll;

    public override bool IsOpenInvite => RawFields.ContainsKey("start");
}

/// <summary>A solver's raw contribution to a Word Hunt game: the words it found and their score.</summary>
public sealed record WordHuntMove(IReadOnlyList<string> Words, int Score);

internal sealed class WordHuntGame : GamePigeonGameParserBase<WordHuntState>, IGamePigeonMoveHandler<WordHuntState, WordHuntMove>
{
    public override string GameKey => "hunt";

    private const string UnclaimedPlayer1Avatar =
        "body,0|eyes,0|mouth,0|acc,0|wins,0|bg_color,0.900000,0.900000,0.900000|" +
        "body_color,0.000000,1.000000,0.000000|glasses,0|stache,0|backdrop,0|hair,0|" +
        "clothes,0|hair_color,0.000000,0.000000,0.000000|clothes_color,0.000000,0.000000,0.000000";

    /// <summary>
    /// Word Hunt is a race, not a turn ping-pong: whoever answers the open invite first
    /// just fills slot 2 and leaves player1/player2 untouched (they stay whatever the
    /// inviter's client already put there). Only whoever finishes second (the other side's
    /// words are already present) claims player1/slot 1 and settles the winner.
    /// </summary>
    public WordHuntState ApplyMove(WordHuntState state, string playerUuid, WordHuntMove move)
    {
        var nextNum = (state.MessageNumber ?? 0) + 1;
        var otherAlreadySubmitted = state.WordsList1.Count > 0 || state.WordsList2.Count > 0;

        if (otherAlreadySubmitted)
        {
            var other = state.Score2 ?? 0;
            var result = move.Score == other ? 0 : move.Score > other ? 1 : -1;
            var winner = $"{playerUuid}|{result}";
            return state with
            {
                Player1Id = playerUuid,
                WordsList1 = move.Words,
                Words1 = move.Words.Count,
                Score1 = move.Score,
                Winner = winner,
                MessageNumber = nextNum,
            };
        }

        return state with
        {
            WordsList2 = move.Words,
            Words2 = move.Words.Count,
            Score2 = move.Score,
            MessageNumber = nextNum,
        };
    }

    public override WordHuntState Parse(GamePigeonEnvelope envelope)
    {
        var fields = envelope.Fields;
        return new WordHuntState(
            fields.Get("sender"),
            fields.Get("player1"),
            fields.Get("player2"),
            fields.GetInt("num"),
            fields,
            envelope.SessionId,
            envelope.GameName,
            fields.Get("letters"),
            fields.Get("lang"),
            fields.GetInt("mode"),
            fields.GetInt("score1"),
            fields.GetInt("score2"),
            fields.GetInt("words1"),
            fields.GetInt("words2"),
            SplitWords(fields.Get("words_list1")),
            SplitWords(fields.Get("words_list2")),
            fields.Get("winner"));
    }

    public override IReadOnlyDictionary<string, string> ToFields(WordHuntState state)
    {
        var raw = state.RawFields;
        var fields = new Dictionary<string, string>();

        fields.SetIfNotNull("sender", state.SessionSender);
        fields.SetIfNotNull("player1", state.Player1Id);
        fields["version"] = "0";
        CopyIfPresent(raw, fields, "tver");
        CopyIfPresent(raw, fields, "ios");
        CopyIfPresent(raw, fields, "id");
        fields["game"] = GameKey;
        fields.SetIfNotNull("player2", state.Player2Id);
        fields["lang"] = "gp_en2";
        fields.SetIfNotNull("mode", state.Mode);

        fields["avatar1"] = raw.Get("avatar1") ?? UnclaimedPlayer1Avatar;
        CopyIfPresent(raw, fields, "avatar2");
        fields.SetIfNotNull("score1", state.Score1);
        fields.SetIfNotNull("words1", state.Words1);
        if (state.WordsList1.Count > 0)
        {
            fields["words_list1"] = string.Join('|', state.WordsList1);
        }

        fields.SetIfNotNull("score2", state.Score2);
        fields.SetIfNotNull("words2", state.Words2);
        if (state.WordsList2.Count > 0)
        {
            fields["words_list2"] = string.Join('|', state.WordsList2);
        }

        fields.SetIfNotNull("letters", state.Letters);
        fields.SetIfNotNull("winner", state.Winner);
        fields.SetIfNotNull("num", state.MessageNumber);
        CopyIfPresent(raw, fields, "build");

        return fields;
    }

    private static void CopyIfPresent(IReadOnlyDictionary<string, string> raw, Dictionary<string, string> fields, string key)
    {
        if (raw.TryGetValue(key, out var value))
        {
            fields[key] = value;
        }
    }

    private static IReadOnlyList<string> SplitWords(string? raw) =>
        string.IsNullOrEmpty(raw) ? [] : raw.Split('|', StringSplitOptions.RemoveEmptyEntries);
}
