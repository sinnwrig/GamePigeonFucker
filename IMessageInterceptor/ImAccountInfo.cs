using System.Text;

namespace IMessage;

public sealed record ImAccountInfo(
    string ClassName,
    string UniqueId,
    string LoginImHandle,
    string Aliases,
    string LoginHandles)
{
    public IReadOnlyList<string> AliasList => ParseNsArrayDescription(Aliases);

    public IReadOnlyList<string> LoginHandleList => ParseNsArrayDescription(LoginHandles);

    public static string FormatAsNsArrayDescription(IEnumerable<string> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return "(\n)";
        }

        var sb = new StringBuilder();
        sb.Append("(\n");
        for (var i = 0; i < list.Count; i++)
        {
            sb.Append("    \"");
            sb.Append(list[i].Replace("\\", "\\\\").Replace("\"", "\\\""));
            sb.Append('"');
            sb.Append(i < list.Count - 1 ? ",\n" : "\n");
        }

        sb.Append(')');
        return sb.ToString();
    }

    private static IReadOnlyList<string> ParseNsArrayDescription(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
        {
            trimmed = trimmed[1..^1];
        }

        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];

            if (inQuotes)
            {
                if (c == '\\' && i + 1 < trimmed.Length)
                {
                    current.Append(trimmed[i + 1]);
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                AppendIfNotEmpty(values, current);
            }
            else if (!char.IsWhiteSpace(c))
            {
                current.Append(c);
            }
        }

        AppendIfNotEmpty(values, current);
        return values;
    }

    private static void AppendIfNotEmpty(List<string> values, StringBuilder current)
    {
        var value = current.ToString().Trim();
        if (value.Length > 0)
        {
            values.Add(value);
        }

        current.Clear();
    }
}
