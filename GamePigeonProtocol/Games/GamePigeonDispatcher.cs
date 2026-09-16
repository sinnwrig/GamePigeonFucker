using System.Reflection;
using GamePigeon;
using IMessage;

namespace GamePigeon.Games;

public sealed class GamePigeonDispatcher
{
    private static readonly Lazy<byte[]> Icon = new(() =>
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GamePigeonIcon.jpg")
            ?? throw new FileNotFoundException("Embedded resource not found: GamePigeonIcon.jpg");
        using MemoryStream memory = new();
        stream.CopyTo(memory);
        return memory.ToArray();
    });

    private readonly GamePigeonGameRegistry _registry;
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly Dictionary<Guid, SessionTracker> _sessions = new();

    private sealed class SessionTracker
    {
        public int LastNum;
        public bool LastWasFromMe;
        public int LastRespondedNum = -1;

        /// <summary>
        /// The iMessage GUID of the game's invite (num=1) message. Real clients send every
        /// move as an associated message (type 2) pointing at the invite's balloon, which
        /// is what makes iOS update the existing balloon in place instead of posting -- and
        /// silently dropping -- a standalone duplicate of the session.
        /// </summary>
        public string? RootMessageGuid;
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

            if ((state.IsOpenInvite || state.MessageNumber == 1) && !string.IsNullOrEmpty(message.Guid))
            {
                tracker.RootMessageGuid = message.Guid;
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

    public bool CanRespond(GamePigeonGameState state, bool messageIsFromMe)
    {
        if (state.SessionId is { } dedupSessionId
            && _sessions.TryGetValue(dedupSessionId, out var dedupTracker)
            && state.MessageNumber is { } incomingNum
            && incomingNum <= dedupTracker.LastRespondedNum)
        {
            return false;
        }

        // An open invite is answerable by either side -- including us. When the local
        // user sends an invite from their real client, the bot plays their slot for
        // them. Own non-invite messages stay ineligible (they're our own moves, and
        // answering them would loop).
        return !messageIsFromMe || state.IsOpenInvite;
    }

    /// <summary>
    /// The entry point solvers should use: hand over the raw move you decided to make (words
    /// found, column chosen, etc.) and the protocol resolves player identity, slot assignment,
    /// winner determination, and message sequencing before sending. Solvers never construct a
    /// next <typeparamref name="TState"/> by hand.
    /// </summary>
    public Task<bool> SendMoveAsync<TState, TMove>(
        TState state,
        TMove move,
        MessagingService service,
        string chatIdentifier,
        string playerUuid,
        string? playerAvatar = null,
        string? fallbackText = null)
        where TState : GamePigeonGameState
    {
        if (_registry.FindParser(state.GameKey) is not IGamePigeonMoveHandler<TState, TMove> handler)
        {
            Console.WriteLine($"[Dispatcher] no move handler for gameKey={state.GameKey}");
            return Task.FromResult(false);
        }

        var nextState = handler.ApplyMove(state, playerUuid, playerAvatar, move);
        return SendMoveAsync(nextState, service, chatIdentifier, playerUuid, playerAvatar, fallbackText);
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

        // The wire `sender` field is the author of *this* message -- us. States parsed from
        // inbound messages carry the opponent's id in SessionSender, and ApplyMove
        // implementations don't touch it, so stamp our own id here at the single send
        // choke point. Sending the opponent's id makes the recipient's GamePigeon resolve
        // the message as its own (isMine:/getPlayer:/fullPlayerId:), which is the root
        // cause of balloons that open but never render game state.
        state = state with { SessionSender = playerUuid };

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

        var fields = new Dictionary<string, string>(parser.ToFields(state));

        // Real clients stamp every message with their own iOS version and a fresh random
        // build token; never echo the opponent's (states parsed inbound carry theirs in
        // RawFields, and some ToFields implementations copy them forward).
        fields["ios"] = GamePigeonClientInfo.IosVersion;
        fields["build"] = GamePigeonClientInfo.NewBuildToken();

        if (state.TurnMode == GameTurnMode.Lockstep
            && fields.GetValueOrDefault("player2") != playerUuid && !fields.ContainsKey("player1"))
        {
            fields["player1"] = playerUuid;
            if (!string.IsNullOrEmpty(playerAvatar))
            {
                fields["avatar1"] = playerAvatar;
            }
        }

        var caption = fallbackText ?? state.GameName ?? string.Empty;
        var userInfo = new Dictionary<string, string>
        {
            ["image-title"] = string.Empty,
            ["caption"] = caption,
            ["image-subtitle"] = string.Empty,
            ["subcaption"] = string.Empty,
            ["tertiary-subcaption"] = string.Empty,
            ["secondary-subcaption"] = string.Empty,
        };

        var envelope = new GamePigeonEnvelope(
            GameName: state.GameName,
            UserInfo: userInfo,
            SessionId: state.SessionId,
            AppName: null,
            AppId: null,
            Thumbnail: Icon.Value,
            DecodedQuery: GamePigeonQueryCodec.BuildQuery(fields),
            Fields: fields);

        // Moves go out as balloon updates associated to the game's invite message
        string? associatedGuid = null;
        if (state.MessageNumber is not (null or 1)
            && state.SessionId is { } assocSessionId
            && _sessions.TryGetValue(assocSessionId, out var assocTracker))
        {
            associatedGuid = assocTracker.RootMessageGuid;
        }

        if (associatedGuid is null && state.MessageNumber is not (null or 1))
        {
            Console.WriteLine($"[Dispatcher] warning: no root message guid for session {state.SessionId}; sending standalone (recipient may ignore it)");
        }

        await service.SendGamePigeonMessageAsync(chatIdentifier, envelope, fallbackText, associatedGuid);

        if (state.SessionId is { } sentSessionId)
        {
            if (!_sessions.TryGetValue(sentSessionId, out var sentTracker))
            {
                sentTracker = new SessionTracker();
                _sessions[sentSessionId] = sentTracker;
            }

            if (state.MessageNumber is { } sentNum)
            {
                if (sentNum > sentTracker.LastNum)
                {
                    sentTracker.LastNum = sentNum;
                }

                if (sentNum > sentTracker.LastRespondedNum)
                {
                    sentTracker.LastRespondedNum = sentNum;
                }
            }

            sentTracker.LastWasFromMe = true;
        }

        return true;
    }
}
