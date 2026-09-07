using IMessage;
using GamePigeon;
using GamePigeon.Games;


public class Gamer
{
    private readonly GamePigeonDispatcher _dispatcher;
    private readonly Dictionary<string, IGameSolver> _solvers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _enabledSolvers = new(StringComparer.OrdinalIgnoreCase);
    private MessagingService? _service;

    public string PlayerUuid { get; }
    public string PlayerAvatar { get; }
    public GamePigeonDispatcher Dispatcher => _dispatcher;
    public MessagingService? Service => _service;
    public IEnumerable<string> SolverNames => _solvers.Keys;

    public Gamer(string playerUuid, string playerAvatar)
    {
        PlayerUuid = playerUuid;
        PlayerAvatar = playerAvatar;
        _dispatcher = new GamePigeonDispatcher(); // GamePigeonGameRegistry.CreateDefault() by default

        RegisterSolver(new ConnectFourSolver());
    }

    private void RegisterSolver(IGameSolver solver)
    {
        _solvers[solver.Name] = solver;
        solver.Register(_dispatcher, this);
    }

    public bool IsEnabled(string name) => _enabledSolvers.Contains(name);

    public bool SetEnabled(string name, bool enabled)
    {
        if (!_solvers.ContainsKey(name))
            return false;

        if (enabled)
            _enabledSolvers.Add(name);
        else
            _enabledSolvers.Remove(name);

        return true;
    }

    public void HandleMessage(InboundMessage message, MessagingService service)
    {
        Console.WriteLine($"[Gamer] HandleMessage chat={message.ChatIdentifier} balloon={message.BalloonBundleId} payloadLen={message.PayloadData?.Length} isFromMe={message.IsFromMe} text={message.Text}");
        _service = service;
        var dispatched = _dispatcher.Dispatch(message);
        Console.WriteLine($"[Gamer] Dispatch returned {dispatched}");
    }
}
