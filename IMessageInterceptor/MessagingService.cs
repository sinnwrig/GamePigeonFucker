namespace IMessage;

public sealed class MessagingService
{
    private readonly IMessageTransport _transport;

    public MessagingService(IMessageTransport transport)
    {
        _transport = transport;
    }

    public static string? DefaultSenderAccountUniqueId { get; set; }
    public static string? DefaultSenderIdentityId { get; set; }

    public event Action<InboundMessage>? OnReceiveMessage;

    public Task StartAsync(CancellationToken cancellationToken) =>
        _transport.WatchAsync(message => OnReceiveMessage?.Invoke(message), cancellationToken);

    public Task SendMessageAsync(string chatIdentifier, OutboundMessage message)
    {
        var resolved = message with
        {
            SenderAccountUniqueId = message.SenderAccountUniqueId ?? DefaultSenderAccountUniqueId,
            SenderIdentityId = message.SenderIdentityId ?? DefaultSenderIdentityId,
        };

        return _transport.SendAsync(chatIdentifier, resolved);
    }

    public Task<IReadOnlyList<ImAccountInfo>> ListAccountsAsync() =>
        _transport.ListAccountsAsync();
}
