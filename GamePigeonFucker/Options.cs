using System.Text.Json;

public sealed class Options
{
    private readonly string _path;
    private readonly Dictionary<string, JsonElement> _values = [];

    public Options(string path)
    {
        _path = path;

        if (!File.Exists(path))
            return;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        foreach (var property in doc.RootElement.EnumerateObject())
            _values[property.Name] = property.Value.Clone();
    }

    public bool GetBool(string name, bool defaultValue = false) =>
        _values.TryGetValue(name, out var v) ? v.GetBoolean() : defaultValue;

    public int GetInt(string name, int defaultValue = 0) =>
        _values.TryGetValue(name, out var v) ? v.GetInt32() : defaultValue;

    public long GetLong(string name, long defaultValue = 0) =>
        _values.TryGetValue(name, out var v) ? v.GetInt64() : defaultValue;

    public float GetFloat(string name, float defaultValue = 0) =>
        _values.TryGetValue(name, out var v) ? v.GetSingle() : defaultValue;

    public double GetDouble(string name, double defaultValue = 0) =>
        _values.TryGetValue(name, out var v) ? v.GetDouble() : defaultValue;

    public string? GetString(string name, string? defaultValue = null) =>
        _values.TryGetValue(name, out var v) ? v.GetString() : defaultValue;

    public void Set(string name, bool value) => Set(name, static (w, v) => w.WriteBooleanValue(v), value);
    public void Set(string name, int value) => Set(name, static (w, v) => w.WriteNumberValue(v), value);
    public void Set(string name, long value) => Set(name, static (w, v) => w.WriteNumberValue(v), value);
    public void Set(string name, float value) => Set(name, static (w, v) => w.WriteNumberValue(v), value);
    public void Set(string name, double value) => Set(name, static (w, v) => w.WriteNumberValue(v), value);
    public void Set(string name, string? value) => Set(name, static (w, v) => w.WriteStringValue(v), value);

    private void Set<T>(string name, Action<Utf8JsonWriter, T> write, T value)
    {
        _values[name] = JsonDocument.Parse(
            GetJsonValue(write, value)).RootElement.Clone();

        Save();
    }

    private static string GetJsonValue<T>(Action<Utf8JsonWriter, T> write, T value)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        write(writer, value);
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private void Save()
    {
        using var stream = File.Create(_path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();

        foreach (var (name, value) in _values)
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}