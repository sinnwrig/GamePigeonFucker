using GamePigeon;
using IMessage;

namespace GamePigeon.Games;

public sealed class GamePigeonDispatcher
{
    private readonly GamePigeonGameRegistry _registry;
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly Dictionary<Guid, SessionTracker> _sessions = new();

    private sealed class SessionTracker
    {
        public int LastNum;
        public bool LastWasFromMe;
    }

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
            return false;
        }

        if (!_registry.TryParse(envelope, out var rawState, out var gameKey) || rawState is not GamePigeonGameState state)
        {
            Console.WriteLine($"[Dispatcher] TryParse failed gameKey={gameKey}");
            return false;
        }

        if (state.SessionId is { } sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var tracker))
            {
                tracker = new SessionTracker();
                _sessions[sessionId] = tracker;
            }

            if (state.MessageNumber is { } num && num > tracker.LastNum)
            {
                tracker.LastNum = num;
            }

            tracker.LastWasFromMe = message.IsFromMe;
        }

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

        if (state.SessionId is { } sessionId && _sessions.TryGetValue(sessionId, out var tracker))
        {
            var expectedNum = tracker.LastNum + 1;
            if (state.MessageNumber != expectedNum)
            {
                Console.WriteLine($"[Dispatcher] refusing to send: session {sessionId} expected message num {expectedNum}, got {state.MessageNumber}");
                return false;
            }

            if (state.TurnMode == GameTurnMode.Lockstep && tracker.LastWasFromMe)
            {
                Console.WriteLine($"[Dispatcher] refusing to send: session {sessionId} is not our turn");
                return false;
            }
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

        if (state.SessionId is { } sentSessionId)
        {
            if (!_sessions.TryGetValue(sentSessionId, out var sentTracker))
            {
                sentTracker = new SessionTracker();
                _sessions[sentSessionId] = sentTracker;
            }

            if (state.MessageNumber is { } sentNum && sentNum > sentTracker.LastNum)
            {
                sentTracker.LastNum = sentNum;
            }

            sentTracker.LastWasFromMe = true;
        }

        return true;
    }
}
