using System.Buffers.Binary;
using System.Text;

namespace GamePigeon.Plist;

internal static class BinaryPropertyListReader
{
    public static object? Read(byte[] data)
    {
        if (data.Length < 40 || Encoding.ASCII.GetString(data, 0, 6) != "bplist")
        {
            throw new FormatException("Not a binary property list");
        }

        var trailer = data.AsSpan(data.Length - 32);
        var offsetIntSize = trailer[6];
        var objectRefSize = trailer[7];
        var numObjects = (long)BinaryPrimitives.ReadUInt64BigEndian(trailer[8..16]);
        var topObject = (long)BinaryPrimitives.ReadUInt64BigEndian(trailer[16..24]);
        var offsetTableOffset = (long)BinaryPrimitives.ReadUInt64BigEndian(trailer[24..32]);

        var offsets = new long[numObjects];
        for (var i = 0; i < numObjects; i++)
        {
            offsets[i] = (long)ReadUIntBE(data, offsetTableOffset + i * offsetIntSize, offsetIntSize);
        }

        var cache = new object?[numObjects];
        var parsed = new bool[numObjects];

        object? ReadObject(long index)
        {
            if (parsed[index])
            {
                return cache[index];
            }

            parsed[index] = true;
            var value = ReadObjectAt(data, offsets[index], objectRefSize, ReadObject);
            cache[index] = value;
            return value;
        }

        return ReadObject(topObject);
    }

    private static object? ReadObjectAt(byte[] data, long offset, byte objectRefSize, Func<long, object?> readObject)
    {
        var marker = data[offset];
        var objType = marker >> 4;
        var objInfo = marker & 0x0F;

        switch (objType)
        {
            case 0x0:
                return objInfo switch
                {
                    0x0 => null,
                    0x8 => false,
                    0x9 => true,
                    _ => throw new FormatException($"Unsupported singleton marker 0x{marker:X2}"),
                };

            case 0x1:
                {
                    var size = 1 << objInfo;
                    var raw = data.AsSpan((int)(offset + 1), size);
                    return size == 8
                        ? BinaryPrimitives.ReadInt64BigEndian(raw)
                        : (long)ReadUIntBE(raw);
                }

            case 0x2:
                {
                    var size = 1 << objInfo;
                    var raw = data.AsSpan((int)(offset + 1), size);
                    return size == 4
                        ? BinaryPrimitives.ReadSingleBigEndian(raw)
                        : BinaryPrimitives.ReadDoubleBigEndian(raw);
                }

            case 0x3:
                {
                    var seconds = BinaryPrimitives.ReadDoubleBigEndian(data.AsSpan((int)(offset + 1), 8));
                    return new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(seconds);
                }

            case 0x4:
                {
                    var (count, headerSize) = ReadCount(data, offset, objInfo);
                    return data.AsSpan((int)(offset + headerSize), (int)count).ToArray();
                }

            case 0x5:
                {
                    var (count, headerSize) = ReadCount(data, offset, objInfo);
                    return Encoding.ASCII.GetString(data, (int)(offset + headerSize), (int)count);
                }

            case 0x6:
                {
                    var (count, headerSize) = ReadCount(data, offset, objInfo);
                    var bytes = data.AsSpan((int)(offset + headerSize), (int)(count * 2));
                    var chars = new char[count];
                    for (var i = 0; i < count; i++)
                    {
                        chars[i] = (char)BinaryPrimitives.ReadUInt16BigEndian(bytes[(i * 2)..]);
                    }

                    return new string(chars);
                }

            case 0x8:
                {
                    var size = objInfo + 1;
                    return new BplistUid(ReadUIntBE(data, offset + 1, size));
                }

            case 0xA:
            case 0xC:
                {
                    var (count, headerSize) = ReadCount(data, offset, objInfo);
                    var list = new List<object?>((int)count);
                    var refsStart = offset + headerSize;
                    for (var i = 0; i < count; i++)
                    {
                        var refIndex = (long)ReadUIntBE(data, refsStart + i * objectRefSize, objectRefSize);
                        list.Add(readObject(refIndex));
                    }

                    return list;
                }

            case 0xD:
                {
                    var (count, headerSize) = ReadCount(data, offset, objInfo);
                    var keysStart = offset + headerSize;
                    var valuesStart = keysStart + count * objectRefSize;
                    var dict = new Dictionary<string, object?>((int)count);
                    for (var i = 0; i < count; i++)
                    {
                        var keyRefIndex = (long)ReadUIntBE(data, keysStart + i * objectRefSize, objectRefSize);
                        var valueRefIndex = (long)ReadUIntBE(data, valuesStart + i * objectRefSize, objectRefSize);
                        var key = readObject(keyRefIndex);
                        var value = readObject(valueRefIndex);
                        dict[key?.ToString() ?? string.Empty] = value;
                    }

                    return dict;
                }

            default:
                throw new FormatException($"Unsupported object type 0x{objType:X} at offset {offset}");
        }
    }

    private static (long Count, long HeaderSize) ReadCount(byte[] data, long offset, int objInfo)
    {
        if (objInfo != 0xF)
        {
            return (objInfo, 1);
        }

        var intMarker = data[offset + 1];
        var intSize = 1 << (intMarker & 0x0F);
        var raw = data.AsSpan((int)(offset + 2), intSize);
        return ((long)ReadUIntBE(raw), 2 + intSize);
    }

    private static ulong ReadUIntBE(byte[] data, long offset, long size) => ReadUIntBE(data.AsSpan((int)offset, (int)size));

    private static ulong ReadUIntBE(ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;
        foreach (var b in bytes)
        {
            value = (value << 8) | b;
        }

        return value;
    }
}
