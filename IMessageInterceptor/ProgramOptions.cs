namespace IMessage;

internal sealed class ProgramOptions
{
    public required string LogPath { get; init; }
    public required SendMode SendMode { get; init; }

    public static ProgramOptions Parse(string[] args)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "imessage-interceptor.log");
        var sendMode = SendMode.Disabled;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--log" when i + 1 < args.Length:
                    logPath = args[++i];
                    break;

                case "--send-mode" when i + 1 < args.Length:
                    sendMode = Enum.Parse<SendMode>(args[++i], ignoreCase: true);
                    break;
            }
        }

        return new ProgramOptions { LogPath = logPath, SendMode = sendMode };
    }
}
