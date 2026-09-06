using System;
using System.Reflection;
using IMessage;


public static class GamePigeonFucker
{
    static MessagingService Service;
    static ImAccountInfo? ActiveAccount;
    static int ActiveAlias;


    public static void Main()
    {
        CommandConsole console = new();

        BannerPrinter.Print("GAME PIGEON FUCKER V1");

        Service = new MessagingService(new ChatDatabaseTransport());

        Service.OnReceiveMessage += LogMessage;

        Console.WriteLine("Initialized IMessage service.");

        console.BeginConsole();
    }


    public static void LogMessage(InboundMessage msg)
    {
        Console.WriteLine($"Message received from: {msg.ChatIdentifier} at {msg.Timestamp} (self?: {msg.IsFromMe})");
        Console.WriteLine($"Bundle: {msg.BalloonBundleId}");
        Console.WriteLine($"> {msg.Text}");
        Console.WriteLine($"Additional info: {msg.Guid} : {msg.HandleId}. {msg.PayloadData?.Length ?? 0} bytes attached");
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
    [CommandParam("-")] Operation type,
    [CommandParam, IntCondition(nameof(type), (long)Operation.find)] string findArg,
    [CommandParam, IntCondition(nameof(type), (long)Operation.set)] string setArg,
    [CommandParam, IntCondition(nameof(type), (long)Operation.set)] string alias
    )
    {
        if (type == Operation.active)
        {
            if (ActiveAccount != null)
                LogAccount(ActiveAccount);
            else
                Console.WriteLine("No active account set");

            return;
        }

        var accounts = await Service.ListAccountsAsync();

        void LogAccount(ImAccountInfo acc)
        {
            Console.WriteLine($"Account: {acc.AliasList[0]}");
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
                    Console.WriteLine($"Setting account: {setAccount.Aliases[0]}");
                    ActiveAccount = setAccount;
                    ActiveAlias = 0;

                    if (!string.IsNullOrEmpty(alias))
                    {
                        for (int i = 0; i < ActiveAccount.AliasList.Count; i++)
                            if (ActiveAccount.AliasList[i] == alias)
                            {
                                ActiveAlias = i;
                                break;
                            }
                    }
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

        await Service.SendMessageAsync(recipient, new OutboundMessage(message, SenderAccountUniqueId: ActiveAccount.UniqueId, SenderIdentityId: ActiveAccount.AliasList[ActiveAlias]));
    }
}
