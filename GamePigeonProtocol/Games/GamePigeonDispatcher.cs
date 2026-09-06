using IMessage;

namespace GamePigeon.Games;

internal sealed class GamePigeonDispatcher
{
    private readonly GamePigeonGameRegistry _registry;
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public GamePigeonDispatcher(GamePigeonGameRegistry? registry = null)
    {
        _registry = registry ?? GamePigeonGameRegistry.CreateDefault();
    }

    public GamePigeonDispatcher OnGame<TState>(Action<TState, InboundMessage> handler)
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

        if (!_registry.TryParse(envelope, out var state, out _) || state is null)
        {
            return false;
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
}
