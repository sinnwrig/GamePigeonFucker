using GamePigeon.Plist;

namespace GamePigeon;

internal static class GamePigeonEnvelopeCodec
{
    private const string DefaultAppName = "GamePigeon";
    private const long DefaultAppId = 1124197642;
    private const string LayoutClass = "MSMessageTemplateLayout";

    public static GamePigeonEnvelope Decode(byte[] payload)
    {
        var root = NSKeyedArchiveDecoder.Decode(payload) as Dictionary<string, object?>
            ?? throw new FormatException("Decoded payload root is not a dictionary");

        var userInfoRaw = root.GetValueOrDefault("userInfo") as Dictionary<string, object?>;
        var userInfo = userInfoRaw?.ToDictionary(kv => kv.Key, kv => kv.Value as string ?? string.Empty)
            ?? new Dictionary<string, string>();

        var relativeUrl = root.GetValueOrDefault("URL") as string;
        var decodedQuery = relativeUrl is null ? string.Empty : GamePigeonQueryCodec.DecodeDataUrl(relativeUrl);
        var fields = GamePigeonQueryCodec.ParseQuery(decodedQuery);

        return new GamePigeonEnvelope(
            GameName: root.GetValueOrDefault("ldtext") as string,
            UserInfo: userInfo,
            SessionId: root.GetValueOrDefault("sessionIdentifier") as Guid?,
            AppName: root.GetValueOrDefault("an") as string,
            AppId: root.GetValueOrDefault("appid") as long?,
            Thumbnail: root.GetValueOrDefault("ai") as byte[],
            DecodedQuery: decodedQuery,
            Fields: fields);
    }

    public static byte[] Encode(GamePigeonEnvelope envelope)
    {
        var builder = new NSKeyedArchiveEncoder();
        var entries = new List<(BplistUid Key, BplistUid Value)>();

        void AddEntry(string key, BplistUid value) => entries.Add((builder.AddString(key), value));

        // Entry order matches real GamePigeon payloads (ai, URL, ldtext, layoutClass,
        // an, sessionIdentifier, userInfo, appid, liveLayoutInfo) — the receiving
        // extension renders the live game view only for payloads that match the
        // genuine client's archive layout (see FINDINGS.md 2.4).
        AddEntry("ai", builder.AddData(envelope.Thumbnail is { Length: > 0 } t ? t : []));
        AddEntry("URL", builder.AddUrl(GamePigeonQueryCodec.EncodeDataUrl(envelope.DecodedQuery)));
        AddEntry("ldtext", builder.AddString(envelope.GameName ?? string.Empty));
        AddEntry("layoutClass", builder.AddString(LayoutClass));
        AddEntry("an", builder.AddString(envelope.AppName ?? DefaultAppName));
        AddEntry("sessionIdentifier", builder.AddUuid(envelope.SessionId ?? Guid.NewGuid()));

        if (envelope.UserInfo.Count > 0)
        {
            var userInfoEntries = envelope.UserInfo
                .Select(kv => (builder.AddString(kv.Key), builder.AddString(kv.Value)))
                .ToList();
            AddEntry("userInfo", builder.AddDictionary("NSDictionary", ["NSDictionary", "NSObject"], userInfoEntries));
        }

        AddEntry("appid", builder.AddNumber(envelope.AppId ?? DefaultAppId));
        AddEntry("liveLayoutInfo", builder.AddData(BuildLiveLayoutInfo()));

        var rootUid = builder.AddDictionary("NSDictionary", ["NSDictionary", "NSObject"], entries);
        return builder.Build(rootUid);
    }

    private static byte[] BuildLiveLayoutInfo()
    {
        var builder = new NSKeyedArchiveEncoder();
        var userInfoUid = builder.AddDictionary("NSDictionary", ["NSDictionary", "NSObject"], []);
        var entries = new List<(BplistUid Key, BplistUid Value)>
        {
            (builder.AddString("layoutClass"), builder.AddString("MSMessageLiveLayout")),
            (builder.AddString("userInfo"), userInfoUid),
        };

        var rootUid = builder.AddDictionary("NSDictionary", ["NSDictionary", "NSObject"], entries);
        return builder.Build(rootUid);
    }
}
