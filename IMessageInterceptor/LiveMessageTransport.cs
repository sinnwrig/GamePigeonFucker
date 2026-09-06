namespace IMessage;

public class LiveMessageTransport : IMessageTransport
{
    private readonly InjectorClient _injector;

    public LiveMessageTransport(string? injectorSocketPath = null)
    {
        _injector = new InjectorClient(injectorSocketPath);
    }

    public Task WatchAsync(Action<InboundMessage> onMessage, CancellationToken cancellationToken) =>
        WatchEventsAsync(chatEvent =>
        {
            if (chatEvent is NewMessageEvent newMessage)
            {
                onMessage(new InboundMessage(
                    Guid: newMessage.MessageGuid,
                    ChatIdentifier: newMessage.ChatIdentifier ?? "",
                    HandleId: newMessage.SenderHandleId ?? "",
                    Text: newMessage.Text ?? "",
                    BalloonBundleId: newMessage.BalloonBundleId ?? "",
                    PayloadData: newMessage.PayloadData,
                    Timestamp: new DateTimeOffset(newMessage.Timestamp, TimeSpan.Zero),
                    IsFromMe: newMessage.IsFromMe));
            }
        }, cancellationToken);

    public async Task WatchEventsAsync(Action<ChatEvent> onEvent, CancellationToken cancellationToken)
    {
        await foreach (var rawEvent in _injector.SubscribeAsync(cancellationToken))
        {
            var chatEvent = ChatEventClassifier.Classify(rawEvent);
            if (chatEvent is not null)
            {
                onEvent(chatEvent);
            }
        }
    }

    public Task SendAsync(string chatIdentifier, OutboundMessage message)
    {
        if (message.SenderAccountUniqueId is { Length: > 0 } accountUniqueId)
        {
            if (message.RawPayload is not null || message.BalloonBundleId is not null)
            {
                throw new NotSupportedException(
                    "sendViaAccount only supports plain text - balloon/payload sends must go through the default account");
            }

            return _injector.SendViaAccountAsync(accountUniqueId, chatIdentifier, message.Text, message.SenderIdentityId);
        }

        var chatGuid = BuildChatGuid(chatIdentifier);
        return _injector.SendAsync(chatGuid, message.Text, message.BalloonBundleId, message.RawPayload);
    }

    public Task<IReadOnlyList<ImAccountInfo>> ListAccountsAsync() => _injector.ListAccountsAsync();

    private static string BuildChatGuid(string chatIdentifier)
    {
        var isGroupChat = chatIdentifier.StartsWith("chat", StringComparison.Ordinal);
        var separator = isGroupChat ? "+" : "-";
        return $"iMessage;{separator};{chatIdentifier}";
    }
}
