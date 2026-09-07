using GamePigeon;
using IMessage;

namespace GamePigeon.Games;

public sealed class GamePigeonDispatcher
{
    private readonly GamePigeonGameRegistry _registry;
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public GamePigeonDispatcher()
    {
        _registry = GamePigeonGameRegistry.CreateDefault();
    }

    internal GamePigeonDispatcher(GamePigeonGameRegistry registry)
    {
        _registry = registry;
    }

    public GamePigeonDispatcher OnGame<TState>(Action<TState, InboundMessage> handler)
        where TState : GamePigeonGameState
    {
        if (!_handlers.TryGetValue(typeof(TState), out var list))
        {
            list = [];
            _handlers[typeof(TState)] = list;
        }

        list.Add(handler);
        return this;
    }

    public bool Dispatch(InboundMessage message)
    {
        if (!message.TryDecodeGamePigeon(out var envelope))
        {
            Console.WriteLine("[Dispatcher] TryDecodeGamePigeon failed");
            return false;
        }

        Console.WriteLine($"[Dispatcher] decoded envelope gameName={envelope.GameName} appId={envelope.AppId} appName={envelope.AppName}");

        if (!_registry.TryParse(envelope, out var state, out var gameKey) || state is null)
        {
            Console.WriteLine($"[Dispatcher] TryParse failed gameKey={gameKey}");
            return false;
        }

        Console.WriteLine($"[Dispatcher] parsed state type={state.GetType().Name} hasHandlers={_handlers.ContainsKey(state.GetType())}");

        if (_handlers.TryGetValue(state.GetType(), out var list))
        {
            foreach (var handler in list)
            {
                handler.DynamicInvoke(state, message);
            }
        }

        return true;
    }

    public async Task<bool> SendMoveAsync<TState>(
        TState state,
        MessagingService service,
        string chatIdentifier,
        string playerUuid,
        string? playerAvatar = null,
        string? fallbackText = null)
        where TState : GamePigeonGameState
    {
        if (_registry.FindParser(state.GameKey) is not IGamePigeonGameParser<TState> parser)
        {
            return false;
        }

        var fields = parser.ToFields(state);
        if (fields.GetValueOrDefault("player2") != playerUuid && !fields.ContainsKey("player1"))
        {
            var claimed = new Dictionary<string, string>(fields) { ["player1"] = playerUuid };
            if (!string.IsNullOrEmpty(playerAvatar))
            {
                claimed["avatar1"] = playerAvatar;
            }

            fields = claimed;
        }

        var envelope = new GamePigeonEnvelope(
            GameName: state.GameName,
            UserInfo: new Dictionary<string, string>(),
            SessionId: state.SessionId,
            AppName: null,
            AppId: null,
            Thumbnail: null,
            DecodedQuery: GamePigeonQueryCodec.BuildQuery(fields),
            Fields: fields);

        await service.SendGamePigeonMessageAsync(chatIdentifier, envelope, fallbackText);
        return true;
    }
}
