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

    /// <summary>Delivery-status updates for outgoing messages (invites' sends settle here).</summary>
    public event Action<DeliveryStatusEvent>? OnDeliveryStatus;

    // Single subscription: two concurrent watches on one transport would each run the
    // shared classifier and dedupe each other, so everything is mapped from one watch.
    public Task StartAsync(CancellationToken cancellationToken) =>
        _transport.WatchEventsAsync(chatEvent =>
        {
            switch (chatEvent)
            {
                case NewMessageEvent m:
                    OnReceiveMessage?.Invoke(new InboundMessage(
                        m.MessageGuid, m.ChatIdentifier ?? "", m.SenderHandleId ?? "", m.Text ?? "",
                        m.BalloonBundleId ?? "", m.PayloadData, new DateTimeOffset(m.Timestamp, TimeSpan.Zero), m.IsFromMe));
                    break;
                case DeliveryStatusEvent d:
                    OnDeliveryStatus?.Invoke(d);
                    break;
            }
        }, cancellationToken);

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
