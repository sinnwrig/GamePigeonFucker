namespace GamePigeon.Games;

internal interface IGamePigeonGameParser
{
    string GameKey { get; }

    Type StateType { get; }

    object Parse(GamePigeonEnvelope envelope);

    IReadOnlyDictionary<string, string> ToFields(object state);
}

internal interface IGamePigeonGameParser<TState> : IGamePigeonGameParser
{
    new TState Parse(GamePigeonEnvelope envelope);

    IReadOnlyDictionary<string, string> ToFields(TState state);
}

internal abstract class GamePigeonGameParserBase<TState> : IGamePigeonGameParser<TState>
{
    public abstract string GameKey { get; }

    public abstract TState Parse(GamePigeonEnvelope envelope);

    public abstract IReadOnlyDictionary<string, string> ToFields(TState state);

    Type IGamePigeonGameParser.StateType => typeof(TState);

    object IGamePigeonGameParser.Parse(GamePigeonEnvelope envelope) => Parse(envelope)!;

    IReadOnlyDictionary<string, string> IGamePigeonGameParser.ToFields(object state) => ToFields((TState)state);
}
