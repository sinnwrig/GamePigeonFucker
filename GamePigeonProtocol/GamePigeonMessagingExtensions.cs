using IMessage;

namespace GamePigeon;

internal static class GamePigeonMessagingExtensions
{
    private const string GamePigeonBalloonBundleId =
        "com.apple.messages.MSMessageExtensionBalloonPlugin:EWFNLB79LQ:com.gamerdelights.gamepigeon.ext";

    public static bool IsGamePigeon(this InboundMessage message) =>
        message.BalloonBundleId.Contains("gamepigeon", StringComparison.OrdinalIgnoreCase);

    public static bool TryDecodeGamePigeon(this InboundMessage message, out GamePigeonEnvelope envelope)
    {
        if (message.IsGamePigeon() && message.PayloadData is { Length: > 0 } payload)
        {
            try
            {
                envelope = GamePigeonEnvelopeCodec.Decode(payload);
                return true;
            }
            catch (FormatException)
            {
            }
        }

        envelope = null!;
        return false;
    }

    public static Task SendGamePigeonMessageAsync(
        this MessagingService service,
        string chatIdentifier,
        GamePigeonEnvelope envelope,
        string? fallbackText = null,
        string? associatedMessageGuid = null)
    {
        var payload = GamePigeonEnvelopeCodec.Encode(envelope);

        // Real balloon messages never carry visible body text: moves use U+FFFC, invites carry an empty body.
        var text = envelope.Fields.ContainsKey("start") ? "" : "\uFFFC";
        return service.SendMessageAsync(chatIdentifier,
            new OutboundMessage(text, payload, GamePigeonBalloonBundleId, AssociatedMessageGuid: associatedMessageGuid));
    }
}
