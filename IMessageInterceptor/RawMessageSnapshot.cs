using System.Text.Json;

namespace IMessage;

internal sealed record RawMessageSnapshot(
    string Guid,
    string? Text,
    string? SenderHandleId,
    bool IsFromMe,
    bool IsEmpty,
    bool IsSent,
    bool IsDelivered,
    bool IsRead,
    bool IsFinished,
    double TimeSeconds,
    double TimeDeliveredSeconds,
    double TimeReadSeconds,
    string? BalloonBundleId,
    byte[]? PayloadData,
    bool IsAssociatedMessage,
    string? AssociatedMessageGuid,
    int AssociatedMessageType,
    string? AssociatedMessageEmoji,
    bool IsReply,
    string? ThreadIdentifier,
    bool HasEditedParts,
    double DateEditedSeconds,
    bool HasRetractedParts)
{
    private static readonly DateTime MacEpoch = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public DateTime Timestamp => MacEpoch.AddSeconds(TimeSeconds);
    public DateTime? DeliveredAt => TimeDeliveredSeconds > 0 ? MacEpoch.AddSeconds(TimeDeliveredSeconds) : null;
    public DateTime? ReadAt => TimeReadSeconds > 0 ? MacEpoch.AddSeconds(TimeReadSeconds) : null;
    public DateTime? EditedAt => DateEditedSeconds > 0 ? MacEpoch.AddSeconds(DateEditedSeconds) : null;

    public static RawMessageSnapshot? FromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new RawMessageSnapshot(
            Guid: GetString(element, "guid") ?? "",
            Text: GetString(element, "text"),
            SenderHandleId: GetString(element, "senderHandleId"),
            IsFromMe: GetBool(element, "isFromMe"),
            IsEmpty: GetBool(element, "isEmpty"),
            IsSent: GetBool(element, "isSent"),
            IsDelivered: GetBool(element, "isDelivered"),
            IsRead: GetBool(element, "isRead"),
            IsFinished: GetBool(element, "isFinished"),
            TimeSeconds: GetDouble(element, "timeSeconds"),
            TimeDeliveredSeconds: GetDouble(element, "timeDeliveredSeconds"),
            TimeReadSeconds: GetDouble(element, "timeReadSeconds"),
            BalloonBundleId: GetString(element, "balloonBundleId"),
            PayloadData: GetString(element, "payloadDataBase64") is { Length: > 0 } base64 ? Convert.FromBase64String(base64) : null,
            IsAssociatedMessage: GetBool(element, "isAssociatedMessage"),
            AssociatedMessageGuid: GetString(element, "associatedMessageGuid"),
            AssociatedMessageType: GetInt(element, "associatedMessageType"),
            AssociatedMessageEmoji: GetString(element, "associatedMessageEmoji"),
            IsReply: GetBool(element, "isReply"),
            ThreadIdentifier: GetString(element, "threadIdentifier"),
            HasEditedParts: GetBool(element, "hasEditedParts"),
            DateEditedSeconds: GetDouble(element, "dateEditedSeconds"),
            HasRetractedParts: GetBool(element, "hasRetractedParts"));
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool GetBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static double GetDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0.0;

    private static int GetInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
}
