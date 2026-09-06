using System;
using System.IO;
using System.Reflection;
using Figgle;

public static class BannerPrinter
{
    private const string DefaultFontResource = "Larry3D.flf";

    private static readonly Lazy<FiggleFont> DefaultFont = new(() => LoadEmbeddedFont(DefaultFontResource));


    public static void Print(string text, ConsoleColor color = ConsoleColor.White)
    {
        Print(text, DefaultFont.Value, color);
    }


    public static void Print(string text, FiggleFont font, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = color;
        Console.Write(font.Render(text));
        Console.ResetColor();
    }


    public static FiggleFont LoadEmbeddedFont(string resourceName, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource not found: {resourceName}");

        return FiggleFontParser.Parse(stream, new StringPool());
    }


    public static FiggleFont LoadFontFromFile(FileInfo file)
    {
        using FileStream stream = file.OpenRead();
        return FiggleFontParser.Parse(stream, new StringPool());
    }
}
