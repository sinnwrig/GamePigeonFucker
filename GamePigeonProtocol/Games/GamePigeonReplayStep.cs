namespace GamePigeon.Games;

public sealed record GamePigeonReplayStep(string Kind, string RawValue)
{
    public IReadOnlyList<string> Values => RawValue.Split(',');

    public IReadOnlyList<int> AsInts() => RawValue.Split(',').Select(v => int.TryParse(v, out var i) ? i : 0).ToList();

    public IReadOnlyList<double> AsDoubles() => RawValue.Split(',').Select(v => double.TryParse(v, out var d) ? d : 0).ToList();

    public override string ToString() => $"{Kind}:{RawValue}";
}

internal static class GamePigeonReplayCodec
{
    public static IReadOnlyList<GamePigeonReplayStep> Parse(string? replay)
    {
        if (string.IsNullOrEmpty(replay))
        {
            return [];
        }

        var steps = new List<GamePigeonReplayStep>();
        foreach (var part in replay.Split('|'))
        {
            var colon = part.IndexOf(':');
            steps.Add(colon < 0
                ? new GamePigeonReplayStep(string.Empty, part)
                : new GamePigeonReplayStep(part[..colon], part[(colon + 1)..]));
        }

        return steps;
    }

    public static string Build(IReadOnlyList<GamePigeonReplayStep> steps)
    {
        return string.Join('|', steps.Select(s => s.Kind.Length == 0 ? s.RawValue : $"{s.Kind}:{s.RawValue}"));
    }
}
