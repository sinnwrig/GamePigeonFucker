using System;
using System.Reflection;
using System.Text;
using IMessage;


public static class GamePigeonFucker
{
    public static MessagingService? Service { get; private set; }
    public static Options Options { get; private set; }
    public static string? IncomingChatIdentifier { get; private set; }

    static CancellationTokenSource? WatchCts;
    static SshInjectorTunnel? ActiveTunnel;
    static CommandConsole CConsole;
    static Gamer Gamer;


    public static async Task Main()
    {
        CConsole = new("GPF v0.1.0 - Kai Angulo");
        Options = new("options.json");
        Gamer = new Gamer();

        BannerPrinter.Print("GAME PIGEON FUCKER V1");

        Remote(Options.GetString("Remote", "local") ?? "local");

        Console.WriteLine("Initialized IMessage service.");

        Account.LoadActiveFromOptions(Options);

        CConsole.BeginConsole();
    }


    static void StartWatching()
    {
        WatchCts = new CancellationTokenSource();
        var cts = WatchCts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Service!.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Watch loop stopped: {ex.Message}");
            }
        });
    }


    [Command("/remote", "Reconnect the messaging service to a remote Mac over SSH, or 'local' to reset to the local injector socket")]
    public static async void Remote([CommandParam] string host)
    {

        WatchCts?.Cancel();
        ActiveTunnel?.Dispose();
        ActiveTunnel = null;

        LiveMessageTransport transport;

        if (string.Equals(host, "local", StringComparison.OrdinalIgnoreCase))
        {
            transport = new LiveMessageTransport();
            Console.WriteLine("Reconfigured to local injector socket.");
        }
        else
        {
            try
            {
                ActiveTunnel = await SshInjectorTunnel.StartAsync(host);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to {host}: {ex.Message}");
                return;
            }

            transport = new LiveMessageTransport(ActiveTunnel.LocalSocketPath);
            Console.WriteLine($"Reconfigured to remote injector at {host}.");
        }

        Service = new MessagingService(transport);
        Service.OnReceiveMessage += LogMessage;
        Account.SetActive(null, null);
        StartWatching();

        Options.Set("Remote", host);
    }


    public static void LogMessage(InboundMessage msg)
    {
        Console.WriteLine($"{msg.ChatIdentifier} ({msg.Timestamp}:{(msg.IsFromMe ? 's' : 'o')})");
        Console.WriteLine($"{msg.Text}");

        StringBuilder sb = new();
        void BuildText(string s) => sb.Append(s);

        if (msg.Text.StartsWith('/'))
        {
            CConsole.OnMessage = BuildText;
            IncomingChatIdentifier = msg.ChatIdentifier;

            CConsole.TryExecute(msg.Text, out string? error);
            Console.WriteLine(error);

            IncomingChatIdentifier = null;
            CConsole?.OnMessage = default;
        }

        if (sb.Length > 0 && sb[^1] == '\\')
            sb.Length--;

        string text = sb.ToString().TrimEnd();

        if (text.Length != 0)
            Account.SendMessage(msg.ChatIdentifier, text);

        Gamer.HandleMessage(msg, Service!);
    }
}
