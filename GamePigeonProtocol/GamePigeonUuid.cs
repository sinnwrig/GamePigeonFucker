namespace GamePigeon;

internal static class GamePigeonUuid
{
    public static Guid FromAppleBytes(byte[] bytes)
    {
        if (bytes.Length != 16)
        {
            throw new FormatException($"Expected 16 UUID bytes, got {bytes.Length}");
        }

        return new Guid(
        [
            bytes[3], bytes[2], bytes[1], bytes[0],
            bytes[5], bytes[4],
            bytes[7], bytes[6],
            bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15],
        ]);
    }

    public static byte[] ToAppleBytes(Guid guid)
    {
        var b = guid.ToByteArray();
        return
        [
            b[3], b[2], b[1], b[0],
            b[5], b[4],
            b[7], b[6],
            b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15],
        ];
    }
}
