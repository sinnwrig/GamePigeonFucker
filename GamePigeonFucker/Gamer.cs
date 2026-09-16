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
        GamePigeonClientInfo.IosVersion = GamePigeonFucker.Options.GetString("GamePigeonIosVersion", GamePigeonClientInfo.IosVersion)!;
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

        // Console.WriteLine($"{hack} hax turned {onOff}");
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

        if (_service is null)
        {
            _service = service;
            _service.OnDeliveryStatus += delivery =>
            {
                if (delivery.IsDelivered)
                {
                    _deliveredGuids.Add(delivery.MessageGuid);
                }
            };
        }

        var dispatched = _dispatcher.Dispatch(message);
        Console.WriteLine($"[Gamer] Dispatch returned {dispatched}");
    }

    private readonly HashSet<string> _deliveredGuids = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Wait until the given outgoing message is confirmed delivered. Answering our own
    /// invite means sending a balloon *update* to it, and updates to a still-in-flight
    /// invite fall back to RCS/SMS (losing the balloon entirely), so callers answering
    /// an own invite must hold until its send settles. Returns false on timeout --
    /// callers send anyway rather than never respond.
    /// </summary>
    public async Task<bool> WaitForDeliveryAsync(string guid, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_deliveredGuids.Contains(guid))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return _deliveredGuids.Contains(guid);
    }
}
