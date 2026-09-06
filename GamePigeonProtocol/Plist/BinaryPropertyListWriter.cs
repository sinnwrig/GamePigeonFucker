using System.Text;

namespace GamePigeon.Plist;

internal static class BinaryPropertyListWriter
{
    private static readonly object NullSentinel = new();

    public static byte[] Write(object root)
    {
        var order = new List<object?>();
        var indexOf = new Dictionary<object, int>();
        int? nullIndex = null;

        int Visit(object? node)
        {
            if (node is null)
            {
                if (nullIndex is null)
                {
                    nullIndex = order.Count;
                    order.Add(null);
                    indexOf[NullSentinel] = nullIndex.Value;
                }

                return nullIndex.Value;
            }

            if (indexOf.TryGetValue(node, out var existing))
            {
                return existing;
            }

            var idx = order.Count;
            order.Add(node);
            indexOf[node] = idx;

            switch (node)
            {
                case List<object?> list:
                    foreach (var item in list)
                    {
                        Visit(item);
                    }

                    break;

                case Dictionary<string, object?> dict:
                    foreach (var (key, value) in dict)
                    {
                        Visit(key);
                        Visit(value);
                    }

                    break;
            }

            return idx;
        }

        var rootIndex = Visit(root);
        var objectRefSize = ByteWidthFor(order.Count - 1);

        var body = new List<byte>(Encoding.ASCII.GetBytes("bplist00"));
        var offsets = new long[order.Count];

        for (var i = 0; i < order.Count; i++)
        {
            offsets[i] = body.Count;
            WriteObject(body, order[i], indexOf, objectRefSize);
        }

        var offsetTableOffset = body.Count;
        var offsetIntSize = ByteWidthFor(offsetTableOffset);
        foreach (var offset in offsets)
        {
            WriteUIntBE(body, (ulong)offset, offsetIntSize);
        }

        body.AddRange(new byte[6]);
        body.Add((byte)offsetIntSize);
        body.Add((byte)objectRefSize);
        WriteUIntBE(body, (ulong)order.Count, 8);
        WriteUIntBE(body, (ulong)rootIndex, 8);
        WriteUIntBE(body, (ulong)offsetTableOffset, 8);

        return body.ToArray();
    }

    private static void WriteObject(List<byte> buffer, object? node, Dictionary<object, int> indexOf, int objectRefSize)
    {
        switch (node)
        {
            case null:
                buffer.Add(0x00);
                break;

            case bool b:
                buffer.Add((byte)(b ? 0x09 : 0x08));
                break;

            case BplistUid uid:
                {
                    var size = ByteWidthFor((long)uid.Index);
                    buffer.Add((byte)(0x80 | (size - 1)));
                    WriteUIntBE(buffer, uid.Index, size);
                    break;
                }

            case long l:
                buffer.Add(0x13);
                WriteUIntBE(buffer, unchecked((ulong)l), 8);
                break;

            case int i:
                buffer.Add(0x13);
                WriteUIntBE(buffer, unchecked((ulong)(long)i), 8);
                break;

            case double d:
                buffer.Add(0x23);
                var bits = BitConverter.DoubleToUInt64Bits(d);
                WriteUIntBE(buffer, bits, 8);
                break;

            case byte[] data:
                WriteCountPrefix(buffer, 0x4, data.Length);
                buffer.AddRange(data);
                break;

            case string s:
                {
                    if (s.All(c => c < 128))
                    {
                        WriteCountPrefix(buffer, 0x5, s.Length);
                        buffer.AddRange(Encoding.ASCII.GetBytes(s));
                    }
                    else
                    {
                        WriteCountPrefix(buffer, 0x6, s.Length);
                        foreach (var c in s)
                        {
                            WriteUIntBE(buffer, c, 2);
                        }
                    }

                    break;
                }

            case List<object?> list:
                WriteCountPrefix(buffer, 0xA, list.Count);
                foreach (var item in list)
                {
                    WriteUIntBE(buffer, (ulong)ResolveIndex(item, indexOf), objectRefSize);
                }

                break;

            case Dictionary<string, object?> dict:
                WriteCountPrefix(buffer, 0xD, dict.Count);
                foreach (var (key, _) in dict)
                {
                    WriteUIntBE(buffer, (ulong)ResolveIndex(key, indexOf), objectRefSize);
                }

                foreach (var (_, value) in dict)
                {
                    WriteUIntBE(buffer, (ulong)ResolveIndex(value, indexOf), objectRefSize);
                }

                break;

            default:
                throw new NotSupportedException($"Cannot serialize plist object of type {node.GetType()}");
        }
    }

    private static int ResolveIndex(object? node, Dictionary<object, int> indexOf)
    {
        return indexOf[node ?? NullSentinel];
    }

    private static void WriteCountPrefix(List<byte> buffer, int typeNibble, int count)
    {
        if (count < 15)
        {
            buffer.Add((byte)((typeNibble << 4) | count));
            return;
        }

        buffer.Add((byte)((typeNibble << 4) | 0xF));
        var size = ByteWidthFor(count);
        var sizeExponent = size switch { 1 => 0, 2 => 1, 4 => 2, 8 => 3, _ => throw new InvalidOperationException() };
        buffer.Add((byte)(0x10 | sizeExponent));
        WriteUIntBE(buffer, (ulong)count, size);
    }

    private static int ByteWidthFor(long maxValue)
    {
        if (maxValue <= 0xFF)
        {
            return 1;
        }

        if (maxValue <= 0xFFFF)
        {
            return 2;
        }

        return maxValue <= 0xFFFFFFFF ? 4 : 8;
    }

    private static void WriteUIntBE(List<byte> buffer, ulong value, int size)
    {
        for (var i = size - 1; i >= 0; i--)
        {
            buffer.Add((byte)((value >> (i * 8)) & 0xFF));
        }
    }
}
