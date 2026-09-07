using System;


[AttributeUsage(AttributeTargets.Method)]
public class CommandAttribute : Attribute
{
    public string Name;
    public string HelpText;


    public CommandAttribute(string name, string helptext)
    {
        Name = name;
        HelpText = helptext;
    }
}


[AttributeUsage(AttributeTargets.Parameter)]
public class CommandFlagAttribute : Attribute
{
    public string FlagName;

    public CommandFlagAttribute(string flagName)
    {
        FlagName = flagName;
    }
}


[AttributeUsage(AttributeTargets.Parameter)]
public class CommandParamAttribute : Attribute
{
    public string? ParameterName;
    public int Count;
    public bool Optional;

    public CommandParamAttribute(string? paramName = null, int count = 0, bool optional = false)
    {
        ParameterName = paramName;
        Count = count;
        Optional = optional;
    }
}


public abstract class ConditionAttribute : Attribute
{
    public string Argument;
    public Type[] ValidTypes;

    protected ConditionAttribute(string argument, Type[] validTypes)
    {
        Argument = argument;
        ValidTypes = validTypes;
    }

    public abstract bool Evaluate(object obj);
}


[AttributeUsage(AttributeTargets.Parameter)]
public class BoolConditionAttribute : ConditionAttribute
{
    public bool BoolValue;

    public BoolConditionAttribute(string argument, bool value = true) : base(argument, [typeof(bool)])
    {
        BoolValue = value;
    }


    public override bool Evaluate(object obj)
    {
        return (bool)obj == BoolValue;
    }
}


[AttributeUsage(AttributeTargets.Parameter)]
public class IntConditionAttribute : ConditionAttribute
{
    public long IntValue;

    public IntConditionAttribute(string argument, long value = 0) : base(argument, [typeof(int), typeof(short), typeof(sbyte), typeof(long)])
    {
        IntValue = value;
    }


    public override bool Evaluate(object obj)
    {
        return Convert.ToInt64(obj) == IntValue;
    }
}