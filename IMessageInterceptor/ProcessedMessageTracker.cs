namespace IMessage;

internal sealed class ProcessedMessageTracker
{
    private readonly HashSet<string> _seenGuids = [];
    private readonly object _lock = new();

    public bool TryMarkProcessed(string guid)
    {
        lock (_lock)
        {
            return _seenGuids.Add(guid);
        }
    }
}
