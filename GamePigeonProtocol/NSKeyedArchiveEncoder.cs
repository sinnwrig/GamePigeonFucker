using GamePigeon.Plist;

namespace GamePigeon;

internal sealed class NSKeyedArchiveEncoder
{
    private readonly List<object?> _objects = ["$null"];
    private readonly Dictionary<string, BplistUid> _classDefs = new();

    public BplistUid AddNull() => new(0);

    public BplistUid AddString(string value) => Add(value);

    public BplistUid AddNumber(long value) => Add(value);

    public BplistUid AddData(byte[] value) => Add(value);

    public BplistUid AddDictionary(string className, string[] hierarchy, IReadOnlyList<(BplistUid Key, BplistUid Value)> entries)
    {
        var classUid = GetOrAddClass(className, hierarchy);
        var keys = new List<object?>();
        var values = new List<object?>();
        foreach (var (key, value) in entries)
        {
            keys.Add(key);
            values.Add(value);
        }

        var node = new Dictionary<string, object?>
        {
            ["$class"] = classUid,
            ["NS.keys"] = keys,
            ["NS.objects"] = values,
        };

        return Add(node);
    }

    public BplistUid AddArray(string className, string[] hierarchy, IReadOnlyList<BplistUid> items)
    {
        var classUid = GetOrAddClass(className, hierarchy);
        var node = new Dictionary<string, object?>
        {
            ["$class"] = classUid,
            ["NS.objects"] = items.Cast<object?>().ToList(),
        };

        return Add(node);
    }

    public BplistUid AddUuid(Guid guid)
    {
        var classUid = GetOrAddClass("NSUUID", ["NSUUID", "NSObject"]);
        var node = new Dictionary<string, object?>
        {
            ["$class"] = classUid,
            ["NS.uuidbytes"] = GamePigeonUuid.ToAppleBytes(guid),
        };

        return Add(node);
    }

    public BplistUid AddUrl(string relative)
    {
        var classUid = GetOrAddClass("NSURL", ["NSURL", "NSObject"]);
        var relativeUid = AddString(relative);
        var node = new Dictionary<string, object?>
        {
            ["$class"] = classUid,
            ["NS.base"] = AddNull(),
            ["NS.relative"] = relativeUid,
        };

        return Add(node);
    }

    public byte[] Build(BplistUid root)
    {
        // Objects serialize in encounter order: NSKeyedUnarchiver is object-order
        // agnostic (real payloads from the same device appear with different
        // root-dict AND object-table orderings and all render live — see
        // FINDINGS.md "Ruled out"), so ordering needs no special handling. The
        // writer's dedup/minimal-width behavior keeps output byte-comparable
        // with real captures for debugging.
        var envelope = new Dictionary<string, object?>
        {
            ["$version"] = 100000L,
            ["$archiver"] = "NSKeyedArchiver",
            ["$top"] = new Dictionary<string, object?> { ["root"] = root },
            ["$objects"] = _objects,
        };

        return BinaryPropertyListWriter.Write(envelope);
    }

    private BplistUid GetOrAddClass(string className, string[] hierarchy)
    {
        if (_classDefs.TryGetValue(className, out var existing))
        {
            return existing;
        }

        var dict = new Dictionary<string, object?>
        {
            ["$classes"] = hierarchy.Cast<object?>().ToList(),
            ["$classname"] = className,
        };

        var uid = Add(dict);
        _classDefs[className] = uid;
        return uid;
    }

    private BplistUid Add(object? value)
    {
        _objects.Add(value);
        return new BplistUid((ulong)(_objects.Count - 1));
    }
}
