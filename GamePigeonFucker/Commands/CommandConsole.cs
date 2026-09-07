using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;


public class CommandConsole
{
    private static CommandConsole? _current;

    private readonly Dictionary<string, CommandInfo> _enabledCommands = new();
    private readonly List<CommandInfo> _disabledCommands = new();

    private readonly object _consoleLock = new();
    private readonly TextWriter _rawOut;
    private readonly List<string> _history = new();
    private readonly StringBuilder _inputBuffer = new();
    private readonly string _promptText = "> ";
    private int _historyIndex;
    private bool _lineActive;

    private bool _running;
    public Action<string>? OnMessage;

    private static string? _helpPreamble;


    public CommandConsole(string helpPreamble)
    {
        _current = this;
        _helpPreamble = helpPreamble;

        _rawOut = Console.Out;
        Console.SetOut(new InterceptingWriter(this, _rawOut));

        DiscoverCommands();
    }


    public void BeginConsole()
    {
        _running = true;

        while (_running)
        {
            string? line = ReadLine();

            if (line == null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!TryExecute(line, out string? error))
                Console.WriteLine(error);
        }
    }


    private string? ReadLine()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.Write(_promptText);
            return Console.ReadLine();
        }

        lock (_consoleLock)
        {
            _inputBuffer.Clear();
            _historyIndex = _history.Count;
            _lineActive = true;
            RedrawLine();
        }

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            lock (_consoleLock)
            {
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        string result = _inputBuffer.ToString();
                        _lineActive = false;
                        _rawOut.Write(Environment.NewLine);

                        if (!string.IsNullOrWhiteSpace(result) && (_history.Count == 0 || _history[^1] != result))
                            _history.Add(result);

                        return result;

                    case ConsoleKey.Backspace:
                        if (_inputBuffer.Length > 0)
                        {
                            _inputBuffer.Length--;
                            RedrawLine();
                        }

                        break;

                    case ConsoleKey.UpArrow:
                        NavigateHistory(-1);
                        break;

                    case ConsoleKey.DownArrow:
                        NavigateHistory(1);
                        break;

                    case ConsoleKey.Escape:
                        _inputBuffer.Clear();
                        _historyIndex = _history.Count;
                        RedrawLine();
                        break;

                    default:
                        if (!char.IsControl(key.KeyChar))
                        {
                            _inputBuffer.Append(key.KeyChar);
                            RedrawLine();
                        }

                        break;
                }
            }
        }
    }


    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0)
            return;

        int newIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count);

        if (newIndex == _historyIndex)
            return;

        _historyIndex = newIndex;
        _inputBuffer.Clear();

        if (_historyIndex < _history.Count)
            _inputBuffer.Append(_history[_historyIndex]);

        RedrawLine();
    }


    private void RedrawLine()
    {
        ClearCurrentLine();
        _rawOut.Write(_promptText);
        _rawOut.Write(_inputBuffer.ToString());
    }


    private void ClearCurrentLine()
    {
        if (Console.IsOutputRedirected)
            return;

        int width = SafeWindowWidth();
        _rawOut.Write('\r');
        _rawOut.Write(new string(' ', width));
        _rawOut.Write('\r');
    }


    private static int SafeWindowWidth()
    {
        try { return Console.WindowWidth; }
        catch { return 80; }
    }


    private void WriteThroughConsole(TextWriter inner, string text)
    {
        lock (_consoleLock)
        {
            if (_lineActive)
            {
                ClearCurrentLine();
                inner.Write(text);
                RedrawLine();
            }
            else
            {
                inner.Write(text);
            }

            OnMessage?.Invoke(text);
        }
    }


    private class InterceptingWriter : TextWriter
    {
        private readonly CommandConsole _owner;
        private readonly TextWriter _inner;


        public InterceptingWriter(CommandConsole owner, TextWriter inner)
        {
            _owner = owner;
            _inner = inner;
        }


        public override Encoding Encoding => _inner.Encoding;

        public override void Write(char value) => _owner.WriteThroughConsole(_inner, value.ToString());
        public override void Write(string? value) => _owner.WriteThroughConsole(_inner, value ?? string.Empty);
        public override void WriteLine() => _owner.WriteThroughConsole(_inner, Environment.NewLine);
        public override void WriteLine(string? value) => _owner.WriteThroughConsole(_inner, (value ?? string.Empty) + Environment.NewLine);
    }


    public void EndConsole()
    {
        _running = false;
    }



    [Command("/help", "Lists all available commands")]
    public static void Help()
    {
        if (_current == null)
            return;

        int enabled = _current._enabledCommands.Count;
        int disabled = _current._disabledCommands.Count;

        if (!string.IsNullOrWhiteSpace(_helpPreamble))
            Console.WriteLine(_helpPreamble);

        Console.WriteLine($"{enabled + disabled} commands discovered, {enabled} enabled, {disabled} disabled");
        Console.WriteLine("Available commands:");

        foreach (CommandInfo info in _current._enabledCommands.Values)
            Console.WriteLine($"  {info.Command.Name} - {info.Command.HelpText}");
    }


    public bool TryExecute(string line, out string? error)
    {
        string[] tokens = Tokenize(line);

        if (tokens.Length == 0)
        {
            error = null;
            return true;
        }

        string commandName = tokens[0];

        if (!_enabledCommands.TryGetValue(commandName, out CommandInfo? info))
        {
            error = $"Unknown command: {commandName}";
            return false;
        }

        List<string> args = tokens.Skip(1).ToList();

        Dictionary<string, object?> parsedValues = new();

        foreach (ParamSlot slot in info.Slots)
        {
            if (slot.Flag == null)
                continue;

            int index = args.FindIndex(a => a == slot.Flag.FlagName);
            bool present = index >= 0;

            if (present)
                args.RemoveAt(index);

            parsedValues[slot.Parameter.Name!] = present;
        }

        object?[] invokeArgs = new object?[info.Slots.Count];
        int cursor = 0;

        try
        {
            foreach ((ParamSlot slot, int i) in info.Slots.Select((s, i) => (s, i)))
            {
                if (slot.Flag != null)
                {
                    invokeArgs[i] = parsedValues[slot.Parameter.Name!];
                    continue;
                }

                if (slot.Condition != null && !slot.Condition.Evaluate(parsedValues[slot.Condition.Argument]!))
                {
                    object? skipped = GetDefault(slot.Parameter.ParameterType);
                    invokeArgs[i] = skipped;
                    parsedValues[slot.Parameter.Name!] = skipped;
                    continue;
                }

                if (slot.IsArray)
                {
                    int take = slot.Param!.Count == 0 ? args.Count - cursor : slot.Param.Count;

                    if (take < 0 || cursor + take > args.Count)
                    {
                        if (!slot.Param.Optional)
                        {
                            error = $"Not enough arguments for '{slot.Parameter.Name}' in command {commandName}";
                            return false;
                        }

                        object? missing = slot.Parameter.HasDefaultValue
                            ? slot.Parameter.DefaultValue
                            : Array.CreateInstance(slot.ElementType!, 0);

                        invokeArgs[i] = missing;
                        parsedValues[slot.Parameter.Name!] = missing;
                        continue;
                    }

                    Array elements = Array.CreateInstance(slot.ElementType!, take);

                    for (int e = 0; e < take; e++)
                    {
                        if (!TryConvert(args[cursor + e], slot.ElementType!, out object? value))
                        {
                            error = $"Invalid value '{args[cursor + e]}' for '{slot.Parameter.Name}'";
                            return false;
                        }

                        elements.SetValue(value, e);
                    }

                    cursor += take;
                    invokeArgs[i] = elements;
                    parsedValues[slot.Parameter.Name!] = elements;
                }
                else
                {
                    if (cursor >= args.Count)
                    {
                        if (!(slot.Param?.Optional ?? false))
                        {
                            error = $"Missing argument for '{slot.Parameter.Name}' in command {commandName}";
                            return false;
                        }

                        object? missing = slot.Parameter.HasDefaultValue
                            ? slot.Parameter.DefaultValue
                            : GetDefault(slot.Parameter.ParameterType);

                        invokeArgs[i] = missing;
                        parsedValues[slot.Parameter.Name!] = missing;
                        continue;
                    }

                    if (!TryConvert(args[cursor], slot.Parameter.ParameterType, out object? value))
                    {
                        error = $"Invalid value '{args[cursor]}' for '{slot.Parameter.Name}'";
                        return false;
                    }

                    invokeArgs[i] = value;
                    parsedValues[slot.Parameter.Name!] = value;
                    cursor++;
                }
            }
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }

        info.Method.Invoke(null, invokeArgs);

        error = null;
        return true;
    }


    private void DiscoverCommands()
    {
        List<MethodInfo> methods = GetType().Assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(m => m.GetCustomAttribute<CommandAttribute>() != null)
            .ToList();

        List<CommandInfo> found = methods.Select(BuildCommandInfo).ToList();

        foreach (CommandInfo info in found)
        {
            if (info.IsValid && _enabledCommands.ContainsKey(info.Command.Name))
                info.MarkInvalid($"Duplicate command name '{info.Command.Name}'");

            if (info.IsValid)
                _enabledCommands[info.Command.Name] = info;
            else
                _disabledCommands.Add(info);
        }

        LogDiscovery(found);
    }


    private static CommandInfo BuildCommandInfo(MethodInfo method)
    {
        CommandInfo info = new(method, method.GetCustomAttribute<CommandAttribute>()!);

        ParameterInfo[] parameters = method.GetParameters();
        bool sawOptionalPositional = false;

        foreach (ParameterInfo parameter in parameters)
        {
            CommandFlagAttribute? flag = parameter.GetCustomAttribute<CommandFlagAttribute>();
            CommandParamAttribute? param = parameter.GetCustomAttribute<CommandParamAttribute>();
            ConditionAttribute? condition = parameter.GetCustomAttribute<ConditionAttribute>();

            ParamSlot slot = new(parameter, flag, param, condition);

            if (flag != null && param != null)
                info.MarkInvalid($"Parameter '{parameter.Name}' cannot have both CommandFlag and CommandParam");

            if (flag != null && condition != null)
                info.MarkInvalid($"Parameter '{parameter.Name}' cannot have both CommandFlag and BoolCondition");

            if (flag != null && parameter.ParameterType != typeof(bool))
                info.MarkInvalid($"CommandFlagAttribute on non-bool parameter '{parameter.Name}'");

            if (flag != null && info.Slots.Any(s => s.Flag != null && s.Flag.FlagName == flag.FlagName))
                info.MarkInvalid($"Duplicate flag name '{flag.FlagName}'");

            if (parameter.ParameterType.IsArray)
            {
                slot.IsArray = true;
                slot.ElementType = parameter.ParameterType.GetElementType();

                if (param == null)
                    info.MarkInvalid($"Array parameter '{parameter.Name}' requires CommandParamAttribute");
                else if (param.Count < 0)
                    info.MarkInvalid($"CommandParamAttribute on '{parameter.Name}' has a negative count");

                if (flag == null && !IsSupportedType(slot.ElementType!))
                    info.MarkInvalid($"Unsupported element type '{slot.ElementType}' for parameter '{parameter.Name}'");
            }
            else if (param != null && param.Count != 0)
            {
                info.MarkInvalid($"CommandParamAttribute count is only valid on array parameters ('{parameter.Name}')");
            }

            if (!slot.IsArray && flag == null && !IsSupportedType(parameter.ParameterType))
                info.MarkInvalid($"Unsupported parameter type '{parameter.ParameterType}' for parameter '{parameter.Name}'");

            if (condition != null)
            {
                if (condition.Argument == parameter.Name)
                {
                    info.MarkInvalid($"ConditionAttribute on '{parameter.Name}' cannot reference itself");
                }
                else
                {
                    ParameterInfo? conditionParam = parameters
                        .FirstOrDefault(p => p.Name == condition.Argument);

                    bool conditionTypeMatches =
                        conditionParam != null &&
                        (condition.ValidTypes.Contains(conditionParam.ParameterType) ||
                         (conditionParam.ParameterType.IsEnum && condition.ValidTypes.Contains(Enum.GetUnderlyingType(conditionParam.ParameterType))));

                    if (conditionParam == null)
                    {
                        info.MarkInvalid($"ConditionAttribute on '{parameter.Name}' references unknown parameter '{condition.Argument}'");
                    }
                    else if (!conditionTypeMatches)
                    {
                        info.MarkInvalid($"ConditionAttribute on '{parameter.Name}' only accepts types: {string.Join(',', condition.ValidTypes.Select(x => x.Name))} '{condition.Argument}'");
                    }
                    else if (conditionParam.GetCustomAttribute<CommandFlagAttribute>() == null &&
                             conditionParam.Position >= parameter.Position)
                    {
                        info.MarkInvalid($"ConditionAttribute on '{parameter.Name}' references '{condition.Argument}', which must be declared earlier or be a CommandFlag");
                    }
                }
            }

            if (flag == null && condition == null)
            {
                bool isOptionalPositional = param != null && param.Optional;

                if (sawOptionalPositional && !isOptionalPositional)
                    info.MarkInvalid($"Required parameter '{parameter.Name}' cannot follow an optional parameter");

                if (isOptionalPositional)
                    sawOptionalPositional = true;
            }

            info.Slots.Add(slot);
        }

        return info;
    }


    private static void LogDiscovery(List<CommandInfo> found)
    {
        List<CommandInfo> enabled = found.Where(f => f.IsValid).ToList();
        List<CommandInfo> disabled = found.Where(f => !f.IsValid).ToList();

        Console.WriteLine($"[CommandConsole] Found {found.Count} command(s): {string.Join(", ", found.Select(f => f.Command.Name))}");
        Console.WriteLine($"[CommandConsole] Enabled ({enabled.Count}): {string.Join(", ", enabled.Select(f => f.Command.Name))}");

        if (disabled.Count == 0)
        {
            Console.WriteLine("[CommandConsole] Disabled (0): none");
            return;
        }

        Console.WriteLine($"[CommandConsole] Disabled ({disabled.Count}):");

        foreach (CommandInfo info in disabled)
            Console.WriteLine($"    {info.Command.Name}: {string.Join("; ", info.Errors)}");
    }


    private static string[] Tokenize(string line)
    {
        List<string> tokens = new();
        StringBuilder current = new();
        bool inQuotes = false;
        bool tokenStarted = false;

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                tokenStarted = true;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (tokenStarted)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    tokenStarted = false;
                }

                continue;
            }

            current.Append(c);
            tokenStarted = true;
        }

        if (tokenStarted)
            tokens.Add(current.ToString());

        return tokens.ToArray();
    }


    private static bool TryConvert(string raw, Type type, out object? value)
    {
        if (type == typeof(string))
        {
            value = raw;
            return true;
        }

        if (type.IsEnum)
        {
            value = ParseEnum(type, raw);
            return true;
        }

        if (type == typeof(int))
        {
            bool ok = int.TryParse(raw, out int i);
            value = i;
            return ok;
        }

        if (type == typeof(long))
        {
            bool ok = long.TryParse(raw, out long l);
            value = l;
            return ok;
        }

        if (type == typeof(short))
        {
            bool ok = short.TryParse(raw, out short s);
            value = s;
            return ok;
        }

        if (type == typeof(float))
        {
            bool ok = float.TryParse(raw, out float f);
            value = f;
            return ok;
        }

        if (type == typeof(double))
        {
            bool ok = double.TryParse(raw, out double d);
            value = d;
            return ok;
        }

        if (type == typeof(decimal))
        {
            bool ok = decimal.TryParse(raw, out decimal m);
            value = m;
            return ok;
        }

        if (type == typeof(bool))
        {
            bool ok = bool.TryParse(raw, out bool b);
            value = b;
            return ok;
        }

        value = null;
        return false;
    }


    private static object ParseEnum(Type type, string raw)
    {
        string[] parts = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            throw new FormatException($"No enum value provided for type '{type.Name}'");

        string[] names = Enum.GetNames(type);
        ulong combined = 0;

        foreach (string part in parts)
        {
            string? name = names.FirstOrDefault(n => string.Equals(n, part, StringComparison.OrdinalIgnoreCase));

            if (name == null)
                throw new FormatException($"Unknown enum value '{part}' for type '{type.Name}'");

            combined |= Convert.ToUInt64(Enum.Parse(type, name));
        }

        return Enum.ToObject(type, combined);
    }


    private static bool IsSupportedType(Type type)
    {
        return type.IsEnum
            || type == typeof(string)
            || type == typeof(int)
            || type == typeof(long)
            || type == typeof(short)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal)
            || type == typeof(bool);
    }


    private static object? GetDefault(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }


    private class CommandInfo
    {
        public MethodInfo Method;
        public CommandAttribute Command;
        public List<ParamSlot> Slots = new();
        public bool IsValid = true;
        public List<string> Errors = new();


        public CommandInfo(MethodInfo method, CommandAttribute command)
        {
            Method = method;
            Command = command;
        }


        public void MarkInvalid(string reason)
        {
            IsValid = false;
            Errors.Add(reason);
        }
    }


    private class ParamSlot
    {
        public ParameterInfo Parameter;
        public CommandFlagAttribute? Flag;
        public CommandParamAttribute? Param;
        public ConditionAttribute? Condition;
        public bool IsArray;
        public Type? ElementType;


        public ParamSlot(ParameterInfo parameter, CommandFlagAttribute? flag, CommandParamAttribute? param, ConditionAttribute? condition)
        {
            Parameter = parameter;
            Flag = flag;
            Param = param;
            Condition = condition;
        }
    }
}
