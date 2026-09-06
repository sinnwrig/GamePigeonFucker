namespace IMessage;

public abstract record ChatEvent(
    string? ChatIdentifier,
    string? ChatGuid,
    string MessageGuid,
    DateTime Timestamp,
    bool IsFromMe);

public sealed record NewMessageEvent(
    string? ChatIdentifier,
    string? ChatGuid,
    string MessageGuid,
    DateTime Timestamp,
    bool IsFromMe,
    string? Text,
    string? SenderHandleId,
    string? BalloonBundleId,
    byte[]? PayloadData,
    bool IsReply,
    string? ThreadIdentifier)
    : ChatEvent(ChatIdentifier, ChatGuid, MessageGuid, Timestamp, IsFromMe);

public sealed record ReactionEvent(
    string? ChatIdentifier,
    string? ChatGuid,
    string MessageGuid,
    DateTime Timestamp,
    bool IsFromMe,
    string? TargetMessageGuid,
    int AssociatedMessageType,
    string? Emoji)
    : ChatEvent(ChatIdentifier, ChatGuid, MessageGuid, Timestamp, IsFromMe);

public sealed record ReadReceiptEvent(
    string? ChatIdentifier,
    string? ChatGuid,
    string MessageGuid,
    DateTime Timestamp,
    bool IsFromMe,
    DateTime ReadAt)
    : ChatEvent(ChatIdentifier, ChatGuid, MessageGuid, Timestamp, IsFromMe);

public sealed record DeliveryStatusEvent(
    string? ChatIdentifier,
    string? ChatGuid,
    string MessageGuid,
    DateTime Timestamp,
    bool IsFromMe,
    bool IsDelivered,
    DateTime? DeliveredAt)
    : ChatEvent(ChatIdentifier, ChatGuid, MessageGuid, Timestamp, IsFromMe);

public sealed record MessageEditedEvent(
    string? ChatIdentifier,
    string? ChatGuid,
    string MessageGuid,
    DateTime Timestamp,
    bool IsFromMe,
    DateTime EditedAt)
    : ChatEvent(ChatIdentifier, ChatGuid, MessageGuid, Timestamp, IsFromMe);

public sealed record MessageRetractedEvent(
    string? ChatIdentifier,
    string? ChatGuid,
    string MessageGuid,
    DateTime Timestamp,
    bool IsFromMe)
    : ChatEvent(ChatIdentifier, ChatGuid, MessageGuid, Timestamp, IsFromMe);

public sealed record ChatResyncEvent(
    string? ChatIdentifier,
    string? ChatGuid,
    string MessageGuid,
    DateTime Timestamp,
    bool IsFromMe)
    : ChatEvent(ChatIdentifier, ChatGuid, MessageGuid, Timestamp, IsFromMe);
