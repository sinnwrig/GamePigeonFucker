using GamePigeon.Plist;

namespace GamePigeon;

internal static class NSKeyedArchiveDecoder
{
    public static object? Decode(byte[] payload)
    {
        var envelope = BinaryPropertyListReader.Read(payload) as Dictionary<string, object?>
            ?? throw new FormatException("Payload is not a binary plist dictionary");

        if (envelope.GetValueOrDefault("$archiver") as string != "NSKeyedArchiver")
        {
            throw new FormatException("Payload is not an NSKeyedArchiver plist");
        }

        var objects = envelope["$objects"] as List<object?>
            ?? throw new FormatException("Missing $objects array");
        var top = envelope["$top"] as Dictionary<string, object?>
            ?? throw new FormatException("Missing $top dictionary");
        var root = top["root"]
            ?? throw new FormatException("Missing $top.root reference");

        var cache = new Dictionary<int, object?>();
        return Resolve(root, objects, cache);
    }

    private static object? Resolve(object? node, List<object?> objects, Dictionary<int, object?> cache)
    {
        if (node is not BplistUid uid)
        {
            return node;
        }

        var index = (int)uid.Index;
        if (index == 0)
        {
            return null;
        }

        if (cache.TryGetValue(index, out var cached))
        {
            return cached;
        }

        var raw = objects[index];
        return ResolveNode(raw, objects, cache, index);
    }

    private static object? ResolveNode(object? raw, List<object?> objects, Dictionary<int, object?> cache, int selfIndex)
    {
        switch (raw)
        {
            case Dictionary<string, object?> dict:
                return ResolveDictionary(dict, objects, cache, selfIndex);

            case List<object?> list:
                {
                    var resolvedList = new List<object?>(list.Count);
                    cache[selfIndex] = resolvedList;
                    foreach (var item in list)
                    {
                        resolvedList.Add(Resolve(item, objects, cache));
                    }

                    return resolvedList;
                }

            default:
                return raw;
        }
    }

    private static object? ResolveDictionary(
        Dictionary<string, object?> dict,
        List<object?> objects,
        Dictionary<int, object?> cache,
        int selfIndex)
    {
        if (dict.ContainsKey("NS.keys") && dict.ContainsKey("NS.objects"))
        {
            var keys = (List<object?>)dict["NS.keys"]!;
            var values = (List<object?>)dict["NS.objects"]!;
            var resolved = new Dictionary<string, object?>();
            cache[selfIndex] = resolved;
            for (var i = 0; i < keys.Count; i++)
            {
                var key = Resolve(keys[i], objects, cache) as string ?? keys[i]?.ToString() ?? string.Empty;
                resolved[key] = Resolve(values[i], objects, cache);
            }

            return resolved;
        }

        if (dict.ContainsKey("NS.objects"))
        {
            var values = (List<object?>)dict["NS.objects"]!;
            var resolved = new List<object?>(values.Count);
            cache[selfIndex] = resolved;
            foreach (var value in values)
            {
                resolved.Add(Resolve(value, objects, cache));
            }

            return resolved;
        }

        if (dict.TryGetValue("NS.string", out var nsString))
        {
            var resolved = Resolve(nsString, objects, cache) as string;
            cache[selfIndex] = resolved;
            return resolved;
        }

        if (dict.TryGetValue("NS.uuidbytes", out var uuidBytesNode))
        {
            var bytes = Resolve(uuidBytesNode, objects, cache) as byte[]
                ?? throw new FormatException("NS.uuidbytes is not data");
            var resolved = GamePigeonUuid.FromAppleBytes(bytes);
            cache[selfIndex] = resolved;
            return resolved;
        }

        if (dict.ContainsKey("NS.base") && dict.ContainsKey("NS.relative"))
        {
            var baseUrl = Resolve(dict["NS.base"], objects, cache) as string;
            var relative = Resolve(dict["NS.relative"], objects, cache) as string;
            var resolved = baseUrl is null ? relative : baseUrl + relative;
            cache[selfIndex] = resolved;
            return resolved;
        }

        var generic = new Dictionary<string, object?>();
        cache[selfIndex] = generic;
        foreach (var (key, value) in dict)
        {
            if (key is "$class" or "$classes" or "$classname")
            {
                continue;
            }

            generic[key] = Resolve(value, objects, cache);
        }

        return generic;
    }
}
