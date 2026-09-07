namespace GamePigeon.Games;

public sealed record KnockoutState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    Guid? SessionId,
    string? GameName,
    int? Mode,
    IReadOnlyList<GamePigeonReplayStep> Replay)
    : GamePigeonGameState("knock", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields, SessionId, GameName);

internal sealed class KnockoutGame : GamePigeonGameParserBase<KnockoutState>
{
    public override string GameKey => "knock";

    public override KnockoutState Parse(GamePigeonEnvelope envelope)
    {
        var fields = envelope.Fields;
        return new KnockoutState(
            fields.Get("sender"),
            fields.Get("player1"),
            fields.Get("player2"),
            fields.GetInt("num"),
            fields,
            envelope.SessionId,
            envelope.GameName,
            fields.GetInt("mode"),
            GamePigeonReplayCodec.Parse(fields.Get("replay")));
    }

    public override IReadOnlyDictionary<string, string> ToFields(KnockoutState state)
    {
        var fields = new Dictionary<string, string>(state.RawFields) { ["game"] = GameKey };
        fields.SetIfNotNull("sender", state.SessionSender);
        fields.SetIfNotNull("player1", state.Player1Id);
        fields.SetIfNotNull("player2", state.Player2Id);
        fields.SetIfNotNull("num", state.MessageNumber);
        fields.SetIfNotNull("mode", state.Mode);
        if (state.Replay.Count > 0)
        {
            fields["replay"] = GamePigeonReplayCodec.Build(state.Replay);
        }

        return fields;
    }
}
