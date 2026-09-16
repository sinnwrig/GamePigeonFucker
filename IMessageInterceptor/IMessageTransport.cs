namespace IMessage;

public interface IMessageTransport
{
    Task WatchAsync(Action<InboundMessage> onMessage, CancellationToken cancellationToken);

    /// <summary>Full classified event stream (messages, reactions, receipts, delivery status, edits, resyncs).</summary>
    Task WatchEventsAsync(Action<ChatEvent> onEvent, CancellationToken cancellationToken);

    Task SendAsync(string chatIdentifier, OutboundMessage message);

    Task<IReadOnlyList<ImAccountInfo>> ListAccountsAsync();
}
