namespace GamePigeon.Games;

/// <summary>
/// Describes the client the bot pretends to be on the wire. Real GamePigeon clients stamp
/// every outgoing message with their own iOS version and a freshly generated random
/// <c>build</c> token -- carrying either one forward from an inbound message makes the
/// bot's reply a byte-level echo of the opponent's message, so both are (re)generated
/// here at send time instead.
/// </summary>
public static class GamePigeonClientInfo
{
    private const string BuildTokenChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Value for the wire <c>ios</c> field. Should match the iOS version of the account's
    /// real GamePigeon device; observed captures send e.g. <c>26.6.1</c>. Settable by the
    /// host (see <c>GamePigeonIosVersion</c> in GamePigeonFucker's options).
    /// </summary>
    public static string IosVersion { get; set; } = "26.6.1";

    /// <summary>
    /// A fresh random <c>build</c> token. Captured real tokens are 2-25 chars of
    /// [A-Za-z0-9] with no discernible structure (confirmed client-generated, no
    /// cryptographic role), so mirror that distribution.
    /// </summary>
    public static string NewBuildToken()
    {
        var length = Random.Shared.Next(2, 26);
        return string.Create(length, BuildTokenChars, static (span, chars) =>
        {
            foreach (ref var c in span)
            {
                c = chars[Random.Shared.Next(chars.Length)];
            }
        });
    }
}
