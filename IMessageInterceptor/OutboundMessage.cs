namespace IMessage;

public sealed record OutboundMessage(
    string Text,
    byte[]? RawPayload = null,
    string? BalloonBundleId = null,
    string? SenderAccountUniqueId = null,
    string? SenderIdentityId = null);
