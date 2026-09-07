using System.Text.Json;

namespace IMessage;

internal sealed class ChatEventClassifier
{
    private readonly HashSet<string> _seenGuids = new();
    private readonly HashSet<string> _guidsWithPayload = new();

    public ChatEvent? Classify(JsonElement rawEvent)
    {
        if (!rawEvent.TryGetProperty("new", out var newElement))
        {
            return null;
        }

        var newMessage = RawMessageSnapshot.FromJson(newElement);
        if (newMessage is null)
        {
            return null;
        }

        var oldMessage = rawEvent.TryGetProperty("old", out var oldElement)
            ? RawMessageSnapshot.FromJson(oldElement)
            : null;

        var chatIdentifier = GetString(rawEvent, "chatIdentifier");
        var chatGuid = GetString(rawEvent, "chatGuid");

        return Classify(chatIdentifier, chatGuid, newMessage, oldMessage);
    }

    internal ChatEvent Classify(string? chatIdentifier, string? chatGuid, RawMessageSnapshot newMessage, RawMessageSnapshot? oldMessage)
    {
        var isFirstSeen = _seenGuids.Add(newMessage.Guid);
        var hasPayloadNow = newMessage.BalloonBundleId is not null || newMessage.PayloadData is not null;
        var hadPayloadBefore = _guidsWithPayload.Contains(newMessage.Guid);
        if (hasPayloadNow)
        {
            _guidsWithPayload.Add(newMessage.Guid);
        }

        if (isFirstSeen || (hasPayloadNow && !hadPayloadBefore))
        {
            if (newMessage.IsAssociatedMessage)
            {
                return new ReactionEvent(
                    chatIdentifier, chatGuid, newMessage.Guid, newMessage.Timestamp, newMessage.IsFromMe,
                    newMessage.AssociatedMessageGuid, newMessage.AssociatedMessageType, newMessage.AssociatedMessageEmoji);
            }

            return new NewMessageEvent(
                chatIdentifier, chatGuid, newMessage.Guid, newMessage.Timestamp, newMessage.IsFromMe,
                newMessage.Text, newMessage.SenderHandleId, newMessage.BalloonBundleId, newMessage.PayloadData,
                newMessage.IsReply, newMessage.ThreadIdentifier);
        }

        if (oldMessage is null)
        {
            return new ChatResyncEvent(chatIdentifier, chatGuid, newMessage.Guid, newMessage.Timestamp, newMessage.IsFromMe);
        }

        if (newMessage.HasEditedParts && (!oldMessage.HasEditedParts || newMessage.DateEditedSeconds != oldMessage.DateEditedSeconds))
        {
            return new MessageEditedEvent(
                chatIdentifier, chatGuid, newMessage.Guid, newMessage.Timestamp, newMessage.IsFromMe,
                newMessage.EditedAt ?? newMessage.Timestamp);
        }

        if (newMessage.HasRetractedParts && !oldMessage.HasRetractedParts)
        {
            return new MessageRetractedEvent(chatIdentifier, chatGuid, newMessage.Guid, newMessage.Timestamp, newMessage.IsFromMe);
        }

        if (newMessage.TimeReadSeconds != oldMessage.TimeReadSeconds && newMessage.ReadAt is { } readAt)
        {
            return new ReadReceiptEvent(chatIdentifier, chatGuid, newMessage.Guid, newMessage.Timestamp, newMessage.IsFromMe, readAt);
        }

        if (newMessage.IsDelivered != oldMessage.IsDelivered)
        {
            return new DeliveryStatusEvent(
                chatIdentifier, chatGuid, newMessage.Guid, newMessage.Timestamp, newMessage.IsFromMe,
                newMessage.IsDelivered, newMessage.DeliveredAt);
        }

        return new ChatResyncEvent(chatIdentifier, chatGuid, newMessage.Guid, newMessage.Timestamp, newMessage.IsFromMe);
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
