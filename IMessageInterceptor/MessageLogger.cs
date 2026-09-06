using System.Globalization;

namespace IMessage;

internal sealed class MessageLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public MessageLogger(string logFilePath)
    {
        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(logFilePath, append: true) { AutoFlush = true };
    }

    public static string FormatLine(InboundMessage message)
    {
        return string.Create(CultureInfo.InvariantCulture, $"[{message.Timestamp:O}] chat={message.ChatIdentifier} from={message.HandleId} fromMe={message.IsFromMe} guid={message.Guid} balloon={message.BalloonBundleId} text={message.Text}");
    }

    public void Log(InboundMessage message)
    {
        var line = FormatLine(message);

        lock (_lock)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
