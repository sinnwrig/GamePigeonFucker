using System;
using System.Reflection;
using System.Text;
using IMessage;


public static class GamePigeonFucker
{
    static MessagingService? Service;
    static string? ActiveAccount;
    static string? ActiveAlias;
    static CancellationTokenSource? WatchCts;
    static SshInjectorTunnel? ActiveTunnel;
    static CommandConsole CConsole;
    static Options Options;
    static Gamer Gamer;


    public static async Task Main()
    {
        CConsole = new("GPF v0.1.0 - Kai Angulo");
        Options = new("options.json");

        var playerUuid = Options.GetString("GamePigeonPlayerUuid");
        if (string.IsNullOrEmpty(playerUuid))
        {
            playerUuid = Guid.NewGuid().ToString();
            Options.Set("GamePigeonPlayerUuid", playerUuid);
        }

        var playerAvatar = Options.GetString("GamePigeonAvatar", "");

        Gamer = new Gamer(playerUuid, playerAvatar!);

        BannerPrinter.Print("GAME PIGEON FUCKER V1");

        Remote(Options.GetString("Remote", "local") ?? "local");

        Console.WriteLine("Initialized IMessage service.");

        ActiveAccount = Options.GetString("ActiveAccount", "");
        ActiveAlias = Options.GetString("ActiveAlias", "");

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
        ActiveAccount = "";
        ActiveAlias = "";
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

            CConsole.TryExecute(msg.Text, out string? error);
            Console.WriteLine(error);

            CConsole?.OnMessage = default;
        }

        if (sb.Length != 0)
            SendMessage(msg.ChatIdentifier, sb.ToString());

        Gamer.HandleMessage(msg, Service!);
    }


    public enum Operation
    {
        set,
        find,
        list,
        active,
    }


    [Command("/account", "Account utilities")]
    public static async void Account(
    [CommandParam] Operation type,
    [CommandParam, IntCondition(nameof(type), (long)Operation.find)] string findArg,
    [CommandParam, IntCondition(nameof(type), (long)Operation.set)] string setArg,
    [CommandParam(optional: true), IntCondition(nameof(type), (long)Operation.set)] string alias
    )
    {
        if (type == Operation.active)
        {
            if (ActiveAccount != null)
                Console.WriteLine($"Active account alias: {ActiveAlias}");
            else
                Console.WriteLine("No active account set");

            return;
        }

        var accounts = await Service!.ListAccountsAsync();

        void LogAccount(ImAccountInfo acc)
        {
            Console.WriteLine($"Account: {acc.AliasList.ElementAtOrDefault(0)}");
            Console.WriteLine($"\tAliases: {string.Join(',', acc.AliasList)}");
            Console.WriteLine($"\tClass name: {acc.ClassName}");
            Console.WriteLine($"\tLogin Handle: {acc.LoginImHandle}");
            Console.WriteLine($"\tLogin Handles: {string.Join(',', acc.LoginHandleList)}");
            Console.WriteLine($"\tUnique ID: {acc.UniqueId}");
        }

        switch (type)
        {
            case Operation.find:
                var account = accounts.FirstOrDefault(x => x?.AliasList.Contains(findArg) ?? false, null);

                if (account != null)
                    LogAccount(account);
                else
                    Console.WriteLine($"Could not find account with alias matching: {findArg}");

                break;

            case Operation.list:
                Console.WriteLine($"{accounts.Count} accounts found");

                foreach (var acc in accounts)
                    LogAccount(acc);

                break;

            case Operation.set:
                var setAccount = accounts.FirstOrDefault(x => x?.Aliases.Contains(setArg) ?? false, null);

                if (setAccount != null)
                {
                    Console.WriteLine($"Setting account: {setAccount.AliasList.ElementAtOrDefault(0)}");
                    ActiveAccount = setAccount.UniqueId;
                    ActiveAlias = setAccount.AliasList.FirstOrDefault();

                    if (!string.IsNullOrEmpty(alias))
                    {
                        for (int i = 0; i < setAccount.AliasList.Count; i++)
                            if (setAccount.AliasList[i] == alias)
                            {
                                ActiveAlias = setAccount.AliasList[i];
                                break;
                            }
                    }

                    Options.Set("ActiveAccount", setAccount.UniqueId);
                    Options.Set("ActiveAlias", ActiveAlias);
                }
                else
                {
                    Console.WriteLine($"Could not find account with alias: {setArg}");
                }

                break;
        }
    }


    [Command("/message", "It's about sending a message, batman")]
    public static async void SendMessage([CommandParam] string recipient, [CommandParam] string message)
    {
        if (ActiveAccount == null)
        {
            Console.WriteLine("No active account set");
            return;
        }

        await Service!.SendMessageAsync(recipient,
            new OutboundMessage(message, SenderAccountUniqueId: ActiveAccount, SenderIdentityId: ActiveAlias));
    }


    public enum OnOff
    {
        on,
        off
    }


    [Command("/hax", "turns on hax")]
    public static void Hax([CommandParam] OnOff onOff, [CommandParam] string hack)
    {
        if (!Gamer.SetEnabled(hack, onOff == OnOff.on))
        {
            Console.WriteLine($"Unknown solver '{hack}'. Available: {string.Join(", ", Gamer.SolverNames)}");
            return;
        }

        Console.WriteLine($"{hack} hax turned {onOff}");
    }
}
