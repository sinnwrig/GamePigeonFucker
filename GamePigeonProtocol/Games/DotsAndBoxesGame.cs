namespace GamePigeon.Games;

internal sealed record DotsAndBoxesState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    int? Size,
    IReadOnlyList<GamePigeonReplayStep> Replay)
    : GamePigeonGameState("dots", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields);

internal sealed class DotsAndBoxesGame : GamePigeonGameParserBase<DotsAndBoxesState>
{
    public override string GameKey => "dots";

    public override DotsAndBoxesState Parse(GamePigeonEnvelope envelope)
    {
        var fields = envelope.Fields;
        return new DotsAndBoxesState(
            fields.Get("sender"),
            fields.Get("player1"),
            fields.Get("player2"),
            fields.GetInt("num"),
            fields,
            fields.GetInt("size"),
            GamePigeonReplayCodec.Parse(fields.Get("replay")));
    }

    public override IReadOnlyDictionary<string, string> ToFields(DotsAndBoxesState state)
    {
        var fields = new Dictionary<string, string>(state.RawFields) { ["game"] = GameKey };
        fields.SetIfNotNull("sender", state.SessionSender);
        fields.SetIfNotNull("player1", state.Player1Id);
        fields.SetIfNotNull("player2", state.Player2Id);
        fields.SetIfNotNull("num", state.MessageNumber);
        fields.SetIfNotNull("size", state.Size);
        if (state.Replay.Count > 0)
        {
            fields["replay"] = GamePigeonReplayCodec.Build(state.Replay);
        }

        return fields;
    }
}
