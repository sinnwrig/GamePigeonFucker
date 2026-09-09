using IMessage;


public static class Account
{
    public static ImAccountInfo? ActiveAccountInfo { get; private set; }
    public static string? ActiveAlias { get; private set; }

    public static string? ActiveAccount => ActiveAccountInfo?.UniqueId;


    public static void SetActive(ImAccountInfo? account, string? alias)
    {
        ActiveAccountInfo = account;
        ActiveAlias = alias;

        MessagingService.DefaultSenderAccountUniqueId = account?.UniqueId;
        MessagingService.DefaultSenderIdentityId = alias;
    }


    public static void LoadActiveFromOptions(Options options)
    {
        string uniqueId = options.GetString("ActiveAccountUniqueId", "") ?? "";

        if (string.IsNullOrEmpty(uniqueId))
            return;

        ImAccountInfo info = new(
            options.GetString("ActiveAccountClassName", "") ?? "",
            uniqueId,
            options.GetString("ActiveAccountLoginImHandle", "") ?? "",
            options.GetString("ActiveAccountAliases", "") ?? "",
            options.GetString("ActiveAccountLoginHandles", "") ?? "");

        string? alias = options.GetString("ActiveAlias", "");
        SetActive(info, string.IsNullOrEmpty(alias) ? info.AliasList.FirstOrDefault() : alias);
    }


    static void SaveActiveToOptions()
    {
        GamePigeonFucker.Options.Set("ActiveAccountClassName", ActiveAccountInfo?.ClassName ?? "");
        GamePigeonFucker.Options.Set("ActiveAccountUniqueId", ActiveAccountInfo?.UniqueId ?? "");
        GamePigeonFucker.Options.Set("ActiveAccountLoginImHandle", ActiveAccountInfo?.LoginImHandle ?? "");
        GamePigeonFucker.Options.Set("ActiveAccountAliases", ActiveAccountInfo?.Aliases ?? "");
        GamePigeonFucker.Options.Set("ActiveAccountLoginHandles", ActiveAccountInfo?.LoginHandles ?? "");
        GamePigeonFucker.Options.Set("ActiveAlias", ActiveAlias ?? "");
    }


    public enum Operation
    {
        set,
        find,
        list,
        active,
        pick,
    }


    [Command("/account", "Account utilities")]
    public static async void Manage(
    [CommandParam] Operation type,
    [CommandParam, IntCondition(nameof(type), (long)Operation.find)] string findArg,
    [CommandParam, IntCondition(nameof(type), (long)Operation.set)] string setArg,
    [CommandParam(optional: true), IntCondition(nameof(type), (long)Operation.set)] string alias
    )
    {
        if (type == Operation.active)
        {
            if (ActiveAccountInfo != null)
            {
                Console.WriteLine($"Active alias:   {ActiveAlias}");
                Console.WriteLine($"Active account: {ActiveAccountInfo.AliasList.ElementAtOrDefault(0) ?? ActiveAccountInfo.UniqueId}");
                Console.WriteLine($"Unique ID:      {ActiveAccountInfo.UniqueId}");
            }
            else
            {
                Console.WriteLine("No active account set");
            }

            return;
        }

        var accounts = await GamePigeonFucker.Service!.ListAccountsAsync();

        switch (type)
        {
            case Operation.find:
                var account = accounts.FirstOrDefault(x => x?.AliasList.Contains(findArg) ?? false, null);

                if (account != null)
                    LogAccountDetail(account);
                else
                    Console.WriteLine($"Could not find account with alias matching: {findArg}");

                break;

            case Operation.list:
                Console.WriteLine($"{accounts.Count} account(s):");

                for (int i = 0; i < accounts.Count; i++)
                    LogAccountSummary(i, accounts[i]);

                break;

            case Operation.set:
                var setAccount = accounts.FirstOrDefault(x => x?.Aliases.Contains(setArg) ?? false, null);

                if (setAccount != null)
                {
                    string activeAlias = setAccount.AliasList.FirstOrDefault();

                    if (!string.IsNullOrEmpty(alias))
                    {
                        for (int i = 0; i < setAccount.AliasList.Count; i++)
                            if (setAccount.AliasList[i] == alias)
                            {
                                activeAlias = setAccount.AliasList[i];
                                break;
                            }
                    }

                    SetActive(setAccount, activeAlias);
                    SaveActiveToOptions();

                    Console.WriteLine($"Active account set to: {activeAlias}");
                }
                else
                {
                    Console.WriteLine($"Could not find account with alias: {setArg}");
                }

                break;

            case Operation.pick:
                var candidates = accounts
                    .Where(a => a.AliasList.Count > 0)
                    .OrderByDescending(a => a.AliasList.Count)
                    .ToList();

                if (candidates.Count == 0)
                {
                    Console.WriteLine("No accounts with aliases found");
                    break;
                }

                string[] accountLabels = candidates
                    .Select(a => $"{a.AliasList[0]} ({a.AliasList.Count} alias{(a.AliasList.Count == 1 ? "" : "es")})")
                    .ToArray();

                int accountIndex = ConsolePrompts.Selector("Select an account:", accountLabels);
                ImAccountInfo picked = candidates[accountIndex];

                int aliasIndex = picked.AliasList.Count == 1
                    ? 0
                    : ConsolePrompts.Selector("Select an alias:", picked.AliasList.ToArray());

                string pickedAlias = picked.AliasList[aliasIndex];

                SetActive(picked, pickedAlias);
                SaveActiveToOptions();

                Console.WriteLine($"Active account set to: {pickedAlias}");

                break;
        }
    }


    static void LogAccountSummary(int index, ImAccountInfo acc)
    {
        string primary = acc.AliasList.FirstOrDefault() ?? acc.UniqueId;
        Console.WriteLine($"  [{index}] {primary,-30} aliases={acc.AliasList.Count,-3} class={acc.ClassName}");
    }


    static void LogAccountDetail(ImAccountInfo acc)
    {
        Console.WriteLine($"Account: {acc.AliasList.ElementAtOrDefault(0) ?? acc.UniqueId}");
        Console.WriteLine($"  Class name:    {acc.ClassName}");
        Console.WriteLine($"  Unique ID:     {acc.UniqueId}");
        Console.WriteLine($"  Login handle:  {acc.LoginImHandle}");
        Console.WriteLine($"  Aliases:       {string.Join(", ", acc.AliasList)}");
        Console.WriteLine($"  Login handles: {string.Join(", ", acc.LoginHandleList)}");
    }


    [Command("/message", "It's about sending a message, batman")]
    public static async void SendMessage([CommandParam] string recipient, [CommandParam] string message)
    {
        if (ActiveAccount == null)
        {
            Console.WriteLine("No active account set");
            return;
        }

        await GamePigeonFucker.Service!.SendMessageAsync(recipient,
            new OutboundMessage(message, SenderAccountUniqueId: ActiveAccount, SenderIdentityId: ActiveAlias));
    }
}
