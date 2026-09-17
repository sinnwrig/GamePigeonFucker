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
    string? Winner,
    string? Avatar1,
    string? Avatar2)
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

    /// <summary>
    /// Slot ownership, inviter claims player 2, invitee claims player 1.
    /// </summary>
    public WordHuntState ApplyMove(WordHuntState state, string playerUuid, string? playerAvatar, WordHuntMove move)
    {
        var nextNum = (state.MessageNumber ?? 0) + 1;
        var takeSlot1 = state.Player2Id != playerUuid && (state.Player1Id is null || state.Player1Id == playerUuid);

        if (takeSlot1)
        {
            return state with
            {
                Player1Id = playerUuid,
                Avatar1 = playerAvatar ?? state.Avatar1,
                WordsList1 = move.Words,
                Words1 = move.Words.Count,
                Score1 = move.Score,
                Winner = SettleWinner(playerUuid, move.Score, state.Score2),
                MessageNumber = nextNum,
            };
        }

        return state with
        {
            Player2Id = playerUuid,
            Avatar2 = playerAvatar ?? state.Avatar2,
            WordsList2 = move.Words,
            Words2 = move.Words.Count,
            Score2 = move.Score,
            Winner = SettleWinner(playerUuid, move.Score, state.Score1),
            MessageNumber = nextNum,
        };
    }

    private static string? SettleWinner(string playerUuid, int score, int? opposingScore)
    {
        if (opposingScore is not { } other)
        {
            return null;
        }

        return $"{playerUuid}|{(score == other ? 0 : score > other ? 1 : -1)}";
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
            fields.Get("winner"),
            fields.Get("avatar1"),
            fields.Get("avatar2"));
    }

    public override IReadOnlyDictionary<string, string> ToFields(WordHuntState state)
    {
        var raw = state.RawFields;
        var fields = new Dictionary<string, string>();
        var isMove = state.Score1 is not null || state.Score2 is not null;

        fields.SetIfNotNull("sender", state.SessionSender);
        fields["tver"] = raw.Get("tver") ?? "5";

        if (!isMove)
        {
            CopyIfPresent(raw, fields, "start");
            CopyIfPresent(raw, fields, "caption");
            fields["version"] = raw.Get("version") ?? "47";
            CopyIfPresent(raw, fields, "player");
        }
        else
        {
            fields["version"] = "0";
        }

        CopyIfPresent(raw, fields, "id");
        fields.SetIfNotNull("player1", state.Player1Id);
        fields.SetIfNotNull("player2", state.Player2Id);
        fields.SetIfNotNull("letters", state.Letters);
        fields["lang"] = isMove ? "gp_en2" : raw.Get("lang") ?? "en";
        fields.SetIfNotNull("mode", state.Mode);
        fields.SetIfNotNull("avatar1", state.Avatar1);
        fields.SetIfNotNull("avatar2", state.Avatar2);

        if (state.Score1 is not null)
        {
            fields.SetIfNotNull("score1", state.Score1);
            fields.SetIfNotNull("words1", state.Words1);
            fields["words_list1"] = string.Join('|', state.WordsList1);
        }

        if (state.Score2 is not null)
        {
            fields.SetIfNotNull("score2", state.Score2);
            fields.SetIfNotNull("words2", state.Words2);
            fields["words_list2"] = string.Join('|', state.WordsList2);
        }

        fields["game"] = GameKey;

        if (!isMove)
        {
            fields.SetIfNotNull("game_name", state.GameName ?? raw.Get("game_name"));
        }

        fields.SetIfNotNull("winner", state.Winner);
        fields.SetIfNotNull("num", state.MessageNumber);

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
