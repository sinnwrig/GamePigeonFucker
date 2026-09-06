namespace GamePigeon.Games;

internal sealed record MancalaState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    int? Size,
    string? Mode,
    IReadOnlyList<GamePigeonReplayStep> Replay)
    : GamePigeonGameState("mancala", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields);

internal sealed class MancalaGame : GamePigeonGameParserBase<MancalaState>
{
    public override string GameKey => "mancala";

    public override MancalaState Parse(GamePigeonEnvelope envelope)
    {
        var fields = envelope.Fields;
        return new MancalaState(
            fields.Get("sender"),
            fields.Get("player1"),
            fields.Get("player2"),
            fields.GetInt("num"),
            fields,
            fields.GetInt("size"),
            fields.Get("mode"),
            GamePigeonReplayCodec.Parse(fields.Get("replay")));
    }

    public override IReadOnlyDictionary<string, string> ToFields(MancalaState state)
    {
        var fields = new Dictionary<string, string>(state.RawFields) { ["game"] = GameKey };
        fields.SetIfNotNull("sender", state.SessionSender);
        fields.SetIfNotNull("player1", state.Player1Id);
        fields.SetIfNotNull("player2", state.Player2Id);
        fields.SetIfNotNull("num", state.MessageNumber);
        fields.SetIfNotNull("size", state.Size);
        fields.SetIfNotNull("mode", state.Mode);
        if (state.Replay.Count > 0)
        {
            fields["replay"] = GamePigeonReplayCodec.Build(state.Replay);
        }

        return fields;
    }
}
