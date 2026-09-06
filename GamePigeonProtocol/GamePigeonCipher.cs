namespace GamePigeon;

internal sealed class Rand48
{
    private ulong _state;

    public void Seed(uint seed)
    {
        _state = (((ulong)seed << 16) + 0x330E) & 0xFFFFFFFFFFFFUL;
    }

    public double NextDouble()
    {
        _state = (25214903917UL * _state + 11UL) & 0xFFFFFFFFFFFFUL;
        return _state / (double)(1UL << 48);
    }
}

internal static class GamePigeonCipher
{
    public static string Decrypt(string blob)
    {
        var rand = new Rand48();
        rand.Seed(unchecked((uint)(blob.Length * 0xEF)));

        var length = blob.Length;
        var offsets = new int[length];
        var modifier = 0;
        for (var i = 0; i < length; i++)
        {
            offsets[i] = (int)Math.Floor(rand.NextDouble() * (modifier + length));
            modifier--;
        }

        var output = new List<char>(length);
        for (var i = 0; i < length; i++)
        {
            var offset = offsets[length - 1 - i];
            var index = length - i - 1;
            output.Insert(offset, blob[index]);
        }

        return new string(output.ToArray());
    }

    public static string Encrypt(string plaintext)
    {
        var rand = new Rand48();
        rand.Seed(unchecked((uint)(plaintext.Length * 0xEF)));

        var remaining = new List<char>(plaintext);
        var output = new List<char>(plaintext.Length);
        while (remaining.Count > 0)
        {
            var idx = (int)Math.Floor(rand.NextDouble() * remaining.Count);
            output.Add(remaining[idx]);
            remaining.RemoveAt(idx);
        }

        return new string(output.ToArray());
    }
}
