namespace IMessage;

internal enum SendMode
{
    Disabled,
    Confirm,
    Enabled,
}

internal sealed class SendGate
{
    private readonly SendMode _mode;
    private readonly SemaphoreSlim _consoleLock = new(1, 1);

    public SendGate(SendMode mode)
    {
        _mode = mode;
    }

    public async Task<bool> ShouldSendAsync(string chatIdentifier, string previewText)
    {
        switch (_mode)
        {
            case SendMode.Disabled:
                Console.WriteLine($"[DRY-RUN] would send to chat={chatIdentifier}: \"{previewText}\"");
                return false;

            case SendMode.Enabled:
                return true;

            case SendMode.Confirm:
                await _consoleLock.WaitAsync();
                try
                {
                    Console.Write($"[CONFIRM] send to chat={chatIdentifier}: \"{previewText}\"? [y/N] ");
                    var input = Console.ReadLine();
                    return string.Equals(input?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
                }
                finally
                {
                    _consoleLock.Release();
                }

            default:
                return false;
        }
    }
}
