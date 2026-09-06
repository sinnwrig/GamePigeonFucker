namespace IMessage;

public sealed record InboundMessage(
    string Guid,
    string ChatIdentifier,
    string HandleId,
    string Text,
    string BalloonBundleId,
    byte[]? PayloadData,
    DateTimeOffset Timestamp,
    bool IsFromMe);
