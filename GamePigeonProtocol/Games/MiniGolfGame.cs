namespace GamePigeon.Games;

public sealed record MiniGolfState(
    string? SessionSender,
    string? Player1Id,
    string? Player2Id,
    int? MessageNumber,
    IReadOnlyDictionary<string, string> RawFields,
    Guid? SessionId,
    string? GameName,
    int? Mode,
    int? Seed,
    IReadOnlyList<(double X, double Y)> Player1Shots,
    IReadOnlyList<(double X, double Y)> Player2Shots)
    : GamePigeonGameState("golf", SessionSender, Player1Id, Player2Id, MessageNumber, RawFields, SessionId, GameName);

internal sealed class MiniGolfGame : GamePigeonGameParserBase<MiniGolfState>
{
    public override string GameKey => "golf";

    public override MiniGolfState Parse(GamePigeonEnvelope envelope)
    {
        var fields = envelope.Fields;
        return new MiniGolfState(
            fields.Get("sender"),
            fields.Get("player1"),
            fields.Get("player2"),
            fields.GetInt("num"),
            fields,
            envelope.SessionId,
            envelope.GameName,
            fields.GetInt("mode"),
            fields.GetInt("seed"),
            ParseShots(fields.Get("replay")),
            ParseShots(fields.Get("replay2")));
    }

    public override IReadOnlyDictionary<string, string> ToFields(MiniGolfState state)
    {
        var fields = new Dictionary<string, string>(state.RawFields) { ["game"] = GameKey };
        fields.SetIfNotNull("sender", state.SessionSender);
        fields.SetIfNotNull("player1", state.Player1Id);
        fields.SetIfNotNull("player2", state.Player2Id);
        fields.SetIfNotNull("num", state.MessageNumber);
        fields.SetIfNotNull("mode", state.Mode);
        fields.SetIfNotNull("seed", state.Seed);
        fields["replay"] = BuildShots(state.Player1Shots);
        fields["replay2"] = BuildShots(state.Player2Shots);
        return fields;
    }

    private static IReadOnlyList<(double, double)> ParseShots(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return [];
        }

        var shots = new List<(double, double)>();
        foreach (var part in raw.Split('&'))
        {
            var xy = part.Split(',');
            if (xy.Length == 2 && double.TryParse(xy[0], out var x) && double.TryParse(xy[1], out var y))
            {
                shots.Add((x, y));
            }
        }

        return shots;
    }

    private static string BuildShots(IReadOnlyList<(double X, double Y)> shots) =>
        string.Join('&', shots.Select(s => $"{s.X},{s.Y}"));
}
