namespace GamePigeon.Games;

public sealed record ConnectFourState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    Guid? SessionId,
    string? GameName,
    int? Size,
    IReadOnlyList<int>? Board,
    (int Column, int Row, int Player)? LastMove,
    string? WinnerId,
    int? WinnerSlot)
    : GamePigeonGameState("connect", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields, SessionId, GameName)
{
    public override GameTurnMode TurnMode => GameTurnMode.Lockstep;
}

internal sealed class ConnectFourGame : GamePigeonGameParserBase<ConnectFourState>
{
    public override string GameKey => "connect";

    public override ConnectFourState Parse(GamePigeonEnvelope envelope)
    {
        var fields = envelope.Fields;
        var steps = GamePigeonReplayCodec.Parse(fields.Get("replay"));
        var board = steps.FirstOrDefault(s => s.Kind == "board")?.AsInts();
        var moveStep = steps.LastOrDefault(s => s.Kind == "move");
        (int, int, int)? lastMove = null;
        if (moveStep is not null)
        {
            var v = moveStep.AsInts();
            if (v.Count >= 3)
            {
                lastMove = (v[0], v[1], v[2]);
            }
        }

        string? winnerId = null;
        int? winnerSlot = null;
        if (fields.Get("winner") is { } winner)
        {
            var parts = winner.Split('|');
            winnerId = parts[0];
            winnerSlot = parts.Length > 1 && int.TryParse(parts[1], out var s) ? s : null;
        }

        return new ConnectFourState(
            fields.Get("sender"),
            fields.Get("player1"),
            fields.Get("player2"),
            fields.GetInt("num"),
            fields,
            envelope.SessionId,
            envelope.GameName,
            fields.GetInt("size"),
            board,
            lastMove,
            winnerId,
            winnerSlot);
    }

    public override IReadOnlyDictionary<string, string> ToFields(ConnectFourState state)
    {
        var fields = new Dictionary<string, string>(state.RawFields) { ["game"] = GameKey };
        fields.SetIfNotNull("sender", state.SessionSender);
        fields.SetIfNotNull("player1", state.Player1Id);
        fields.SetIfNotNull("player2", state.Player2Id);
        fields.SetIfNotNull("num", state.MessageNumber);
        fields.SetIfNotNull("size", state.Size);

        var steps = new List<GamePigeonReplayStep>();
        if (state.Board is not null)
        {
            steps.Add(new GamePigeonReplayStep("board", string.Join(',', state.Board)));
        }

        if (state.LastMove is { } move)
        {
            steps.Add(new GamePigeonReplayStep("move", $"{move.Column},{move.Row},{move.Player}"));
        }

        if (steps.Count > 0)
        {
            fields["replay"] = GamePigeonReplayCodec.Build(steps);
        }

        if (state.WinnerId is not null)
        {
            fields["winner"] = state.WinnerSlot is { } slot ? $"{state.WinnerId}|{slot}" : state.WinnerId;
        }

        return fields;
    }
}
