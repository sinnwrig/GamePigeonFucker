namespace GamePigeon.Games;

public sealed record CupPongState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    Guid? SessionId,
    string? GameName,
    string? Mode,
    int? Seed,
    int? Seed2,
    int? Round,
    int? Score1,
    int? Score2,
    IReadOnlyList<GamePigeonReplayStep> Replay)
    : GamePigeonGameState("beer", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields, SessionId, GameName);

internal sealed class CupPongGame : GamePigeonGameParserBase<CupPongState>
{
    public override string GameKey => "beer";

    public override CupPongState Parse(GamePigeonEnvelope envelope)
    {
        var fields = envelope.Fields;
        return new CupPongState(
            fields.Get("sender"),
            fields.Get("player1"),
            fields.Get("player2"),
            fields.GetInt("num"),
            fields,
            envelope.SessionId,
            envelope.GameName,
            fields.Get("mode"),
            fields.GetInt("seed"),
            fields.GetInt("seed2"),
            fields.GetInt("round"),
            fields.GetInt("score1"),
            fields.GetInt("score2"),
            GamePigeonReplayCodec.Parse(fields.Get("replay")));
    }

    public override IReadOnlyDictionary<string, string> ToFields(CupPongState state)
    {
        var fields = new Dictionary<string, string>(state.RawFields) { ["game"] = GameKey };
        fields.SetIfNotNull("sender", state.SessionSender);
        fields.SetIfNotNull("player1", state.Player1Id);
        fields.SetIfNotNull("player2", state.Player2Id);
        fields.SetIfNotNull("num", state.MessageNumber);
        fields.SetIfNotNull("mode", state.Mode);
        fields.SetIfNotNull("seed", state.Seed);
        fields.SetIfNotNull("seed2", state.Seed2);
        fields.SetIfNotNull("round", state.Round);
        fields.SetIfNotNull("score1", state.Score1);
        fields.SetIfNotNull("score2", state.Score2);
        if (state.Replay.Count > 0)
        {
            fields["replay"] = GamePigeonReplayCodec.Build(state.Replay);
        }

        return fields;
    }
}
