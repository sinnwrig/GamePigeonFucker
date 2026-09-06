namespace IMessage;

public interface IMessageTransport
{
    Task WatchAsync(Action<InboundMessage> onMessage, CancellationToken cancellationToken);

    Task SendAsync(string chatIdentifier, OutboundMessage message);

    Task<IReadOnlyList<ImAccountInfo>> ListAccountsAsync();
}
