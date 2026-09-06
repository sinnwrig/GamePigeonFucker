namespace GamePigeon;

internal sealed record GamePigeonEnvelope(
    string? GameName,
    IReadOnlyDictionary<string, string> UserInfo,
    Guid? SessionId,
    string? AppName,
    long? AppId,
    byte[]? Thumbnail,
    string DecodedQuery,
    IReadOnlyDictionary<string, string> Fields);
