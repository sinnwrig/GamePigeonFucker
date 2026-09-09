using System;
using System.Reflection;
using System.Security.Cryptography;
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

    const int MaxWatchAttempts = 4;
    static readonly TimeSpan WatchRetryDelay = TimeSpan.FromSeconds(3);


    public static async Task Main()
    {
        CConsole = new("GPF v0.1.0 - Kai Angulo");
        Options = new("options.json");
        Gamer = new Gamer();

        BannerPrinter.Print("GAME PIGEON FUCKER V1");

        await Remote(Options.GetString("Remote", "local") ?? "local");

        Console.WriteLine("Initialized IMessage service.");

        Account.LoadActiveFromOptions(Options);

        CConsole.BeginConsole();
    }


    static async Task<bool> TryConnectAsync(string host, CancellationToken cancellationToken)
    {
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
                ActiveTunnel = await SshInjectorTunnel.StartAsync(host, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to {host}: {ex.Message}");
                return false;
            }

            transport = new LiveMessageTransport(ActiveTunnel.LocalSocketPath);
            Console.WriteLine($"Reconfigured to remote injector at {host}.");
        }

        Service = new MessagingService(transport);
        Service.OnReceiveMessage += LogMessage;
        Account.SetActive(null, null);
        return true;
    }


    static async Task WatchLoopAsync(string host, CancellationTokenSource cts)
    {
        var attempt = 1;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                await Service!.StartAsync(cts.Token);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (cts.IsCancellationRequested)
                    return;

                if (attempt >= MaxWatchAttempts)
                {
                    Console.WriteLine($"Watch loop to {host} failed after {attempt} attempts, giving up: {ex.Message}");
                    return;
                }

                Console.WriteLine($"Watch loop to {host} stopped (attempt {attempt}/{MaxWatchAttempts}): {ex.Message}. Reconnecting in {WatchRetryDelay.TotalSeconds:0}s...");

                ActiveTunnel?.Dispose();
                ActiveTunnel = null;

                try
                {
                    await Task.Delay(WatchRetryDelay, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                attempt++;

                if (!await TryConnectAsync(host, cts.Token))
                {
                    Console.WriteLine($"Reconnect attempt {attempt}/{MaxWatchAttempts} to {host} failed, giving up.");
                    return;
                }
            }
        }
    }


    [Command("/remote", "Reconnect the messaging service to a remote Mac over SSH, or 'local' to reset to the local injector socket")]
    public static async Task Remote([CommandParam] string host)
    {
        WatchCts?.Cancel();
        ActiveTunnel?.Dispose();
        ActiveTunnel = null;

        WatchCts = new CancellationTokenSource();
        var cts = WatchCts;

        if (!await TryConnectAsync(host, cts.Token))
        {
            return;
        }

        Options.Set("Remote", host);

        _ = Task.Run(() => WatchLoopAsync(host, cts));
    }


    private static readonly ConsoleColor[] _pallette =
    {
        ConsoleColor.Blue,
        ConsoleColor.Green,
        ConsoleColor.Cyan,
        ConsoleColor.Red,
        ConsoleColor.Magenta,
        ConsoleColor.Yellow,
    };


    private static ConsoleColor GetColorForID(string inboundMsg)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(inboundMsg));
        uint n = BitConverter.ToUInt32(hash, 0);
        return _pallette[(int)(n % (uint)_pallette.Length)];
    }


    public static void LogMessage(InboundMessage msg)
    {
        Console.WriteLine($"{msg.ChatIdentifier} ({msg.Timestamp}):");
        Console.ForegroundColor = GetColorForID(msg.IsFromMe ? Account.ActiveAlias ?? msg.ChatIdentifier : msg.ChatIdentifier);
        Console.WriteLine($"{msg.Text}");
        Console.ForegroundColor = ConsoleColor.Gray;

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
