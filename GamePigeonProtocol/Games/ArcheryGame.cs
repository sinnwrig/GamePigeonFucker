namespace GamePigeon.Games;

internal sealed record ArcheryState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    int? Seed,
    IReadOnlyList<GamePigeonReplayStep> Replay)
    : GamePigeonGameState("archery", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields);

internal sealed class ArcheryGame : GamePigeonGameParserBase<ArcheryState>
{
    public override string GameKey => "archery";

    public override ArcheryState Parse(GamePigeonEnvelope envelope)
    {
        var fields = envelope.Fields;
        return new ArcheryState(
            fields.Get("sender"),
            fields.Get("player1"),
            fields.Get("player2"),
            fields.GetInt("num"),
            fields,
            fields.GetInt("seed"),
            GamePigeonReplayCodec.Parse(fields.Get("replay")));
    }

    public override IReadOnlyDictionary<string, string> ToFields(ArcheryState state)
    {
        var fields = new Dictionary<string, string>(state.RawFields) { ["game"] = GameKey };
        fields.SetIfNotNull("sender", state.SessionSender);
        fields.SetIfNotNull("player1", state.Player1Id);
        fields.SetIfNotNull("player2", state.Player2Id);
        fields.SetIfNotNull("num", state.MessageNumber);
        fields.SetIfNotNull("seed", state.Seed);
        if (state.Replay.Count > 0)
        {
            fields["replay"] = GamePigeonReplayCodec.Build(state.Replay);
        }

        return fields;
    }
}
