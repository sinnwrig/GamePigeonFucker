using IMessage;
using GamePigeon;
using GamePigeon.Games;


public class Gamer
{
    private readonly GamePigeonDispatcher _dispatcher;
    private readonly Dictionary<string, IGameSolver> _solvers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _enabledSolvers = new(StringComparer.OrdinalIgnoreCase);
    private MessagingService? _service;

    public static Gamer? Instance { get; private set; }

    public string PlayerUuid { get; }
    public string PlayerAvatar { get; }
    public GamePigeonDispatcher Dispatcher => _dispatcher;
    public MessagingService? Service => _service;
    public IEnumerable<string> SolverNames => _solvers.Keys;


    private string DefaultUUID() => GamePigeonFucker.Options.GetString("GamePigeonPlayerUuid", Guid.NewGuid().ToString())!;
    private string DefaultAvatar() => GamePigeonFucker.Options.GetString("GamePigeonAvatar", "")!;


    public Gamer(string? playerUuid = null, string? playerAvatar = null)
    {
        PlayerUuid = playerUuid ?? DefaultUUID();
        PlayerAvatar = playerAvatar ?? DefaultAvatar();
        _dispatcher = new GamePigeonDispatcher();

        RegisterSolver(new ConnectFourSolver());
        RegisterSolver(new WordHuntSolver());

        Instance = this;
    }


    public enum OnOff
    {
        on,
        off
    }


    [Command("/hax", "turns on hax")]
    public static void Hax([CommandParam] OnOff onOff, [CommandParam] string hack)
    {
        if (Instance == null)
        {
            Console.WriteLine("Gamer not initialized");
            return;
        }

        if (!Instance.SetEnabled(hack, onOff == OnOff.on))
        {
            Console.WriteLine($"Unknown solver '{hack}'. Available: {string.Join(", ", Instance.SolverNames)}");
            return;
        }

        Console.WriteLine($"{hack} hax turned {onOff}");
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
