namespace GamePigeon.Games;

public sealed record ArcheryState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    Guid? SessionId,
    string? GameName,
    int? Seed,
    IReadOnlyList<GamePigeonReplayStep> Replay)
    : GamePigeonGameState("archery", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields, SessionId, GameName);

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
            envelope.SessionId,
            envelope.GameName,
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
