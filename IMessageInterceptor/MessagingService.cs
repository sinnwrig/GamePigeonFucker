namespace IMessage;

public sealed class MessagingService
{
    private readonly IMessageTransport _transport;

    public MessagingService(IMessageTransport transport)
    {
        _transport = transport;
    }

    public event Action<InboundMessage>? OnReceiveMessage;

    public Task StartAsync(CancellationToken cancellationToken) =>
        _transport.WatchAsync(message => OnReceiveMessage?.Invoke(message), cancellationToken);

    public Task SendMessageAsync(string chatIdentifier, OutboundMessage message) =>
        _transport.SendAsync(chatIdentifier, message);
}
