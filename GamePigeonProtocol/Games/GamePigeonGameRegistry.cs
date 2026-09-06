namespace GamePigeon.Games;

internal sealed class GamePigeonGameRegistry
{
    private readonly Dictionary<string, IGamePigeonGameParser> _parsers = new();

    public GamePigeonGameRegistry Register(IGamePigeonGameParser parser)
    {
        _parsers[parser.GameKey] = parser;
        return this;
    }

    public IGamePigeonGameParser? FindParser(string gameKey) => _parsers.GetValueOrDefault(gameKey);

    public bool TryParse(GamePigeonEnvelope envelope, out object? state, out string? gameKey)
    {
        gameKey = envelope.Fields.Get("game");
        if (gameKey is not null && _parsers.TryGetValue(gameKey, out var parser))
        {
            state = parser.Parse(envelope);
            return true;
        }

        state = null;
        return false;
    }

    public bool TryParse<TState>(GamePigeonEnvelope envelope, out TState? state)
    {
        var gameKey = envelope.Fields.Get("game");
        if (gameKey is not null
            && _parsers.TryGetValue(gameKey, out var parser)
            && parser is IGamePigeonGameParser<TState> typed)
        {
            state = typed.Parse(envelope);
            return true;
        }

        state = default;
        return false;
    }

    public static GamePigeonGameRegistry CreateDefault() => new GamePigeonGameRegistry()
        .Register(new ConnectFourGame())
        .Register(new AnagramsGame())
        .Register(new CupPongGame())
        .Register(new WordHuntGame())
        .Register(new MancalaGame())
        .Register(new WordBitesGame())
        .Register(new FillerGame())
        .Register(new ArcheryGame())
        .Register(new KnockoutGame())
        .Register(new MiniGolfGame())
        .Register(new DotsAndBoxesGame());
}
