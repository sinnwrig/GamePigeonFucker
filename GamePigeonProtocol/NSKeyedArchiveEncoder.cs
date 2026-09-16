using GamePigeon.Plist;

namespace GamePigeon;

internal sealed class NSKeyedArchiveEncoder
{
    private readonly List<object?> _objects = ["$null"];
    private readonly Dictionary<string, BplistUid> _classDefs = new();
    private readonly Dictionary<string, BplistUid> _stringCache = new();

    public BplistUid AddNull() => new(0);

    public BplistUid AddString(string value)
    {
        // Foundation dedupes equal strings in the archive (real payloads share a
        // single object for repeated empty strings)
        if (_stringCache.TryGetValue(value, out var existing))
        {
            return existing;
        }

        var uid = Add(value);
        _stringCache[value] = uid;
        return uid;
    }

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
        // Foundation's NSKeyedArchiver serializes the object graph in a specific
        // order, and GP's extension on receiving devices only renders the live
        // game view when the payload follows it (proven 2026-09-16 by A/B replay:
        // real Foundation-encoded payloads render the live game UI; our previous
        // encounter-order encoding rendered the generic fallback card — and even
        // re-serializing our graph with Apple's own NSKeyedArchiver on the Mac
        // failed, because that preserved our ordering and wrapped the inner
        // liveLayoutInfo blob in an NSMutableData object that real payloads
        // don't have).
        //
        // Foundation order: '$null' at 0, then the root dictionary, then — for
        // each dictionary — ALL of its NS.keys entries first, then its NS.objects
        // entries (each recursed depth-first), then its $class definition
        // (deduplicated). Integers use the minimal encoding width.

        var newObjects = new List<object?>(_objects.Count);
        var placed = new HashSet<ulong>();
        var remap = new Dictionary<ulong, int>();

        // '$null' is always object 0.
        placed.Add(0);
        remap[0] = 0;
        newObjects.Add(_objects[0]);

        void Emit(ulong oldIdx)
        {
            if (placed.Contains(oldIdx))
            {
                return;
            }

            placed.Add(oldIdx);
            remap[oldIdx] = newObjects.Count;
            newObjects.Add(_objects[(int)oldIdx]);

            if (_objects[(int)oldIdx] is Dictionary<string, object?> d)
            {
                if (d.TryGetValue("NS.keys", out var ko) && ko is List<object?> keys)
                {
                    foreach (var k in keys)
                    {
                        Emit(((BplistUid)k).Index);
                    }

                    if (d.TryGetValue("NS.objects", out var vo) && vo is List<object?> vals)
                    {
                        foreach (var v in vals)
                        {
                            Emit(((BplistUid)v).Index);
                        }
                    }

                    if (d.TryGetValue("$class", out var c))
                    {
                        Emit(((BplistUid)c).Index);
                    }
                }
                else if (d.TryGetValue("NS.relative", out var rel))
                {
                    Emit(((BplistUid)rel).Index);
                    if (d.TryGetValue("NS.base", out var b))
                    {
                        Emit(((BplistUid)b).Index);
                    }

                    if (d.TryGetValue("$class", out var c))
                    {
                        Emit(((BplistUid)c).Index);
                    }
                }
                else if (d.TryGetValue("NS.uuidbytes", out var ub))
                {
                    if (d.TryGetValue("$class", out var c))
                    {
                        Emit(((BplistUid)c).Index);
                    }
                }
                else if (d.TryGetValue("$classname", out var cn))
                {
                    // class definition: '$classname'/'$classes' are inline
                    // strings/lists — nothing further to recurse
                }
            }
        }

        Emit(root.Index);

        // rewrite every BplistUid reference to its Foundation-order index
        for (var i = 0; i < newObjects.Count; i++)
        {
            if (newObjects[i] is Dictionary<string, object?> d)
            {
                foreach (var k in d.Keys.ToList())
                {
                    if (d[k] is BplistUid u)
                    {
                        d[k] = new BplistUid((ulong)remap[u.Index]);
                    }
                    else if (d[k] is List<object?> list)
                    {
                        for (var j = 0; j < list.Count; j++)
                        {
                            if (list[j] is BplistUid u2)
                            {
                                list[j] = new BplistUid((ulong)remap[u2.Index]);
                            }
                        }
                    }
                }
            }
        }

        var envelope = new Dictionary<string, object?>
        {
            ["$version"] = 100000L,
            ["$archiver"] = "NSKeyedArchiver",
            ["$top"] = new Dictionary<string, object?> { ["root"] = new BplistUid((ulong)remap[root.Index]) },
            ["$objects"] = newObjects,
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
