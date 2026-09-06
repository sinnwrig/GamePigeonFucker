using System;
using System.Reflection;

public static class WordFucker
{
    private static string Dictionary = "Collins Scrabble Words (2019).txt";
    private static WordDefinitions? Definitions;

    private static readonly string[] Banner =
    [
        @" ___       __   ________  ________  ________          ________ ___  ___  ________  ___  __    _______   ________     ",
        @"|\  \     |\  \|\   __  \|\   __  \|\   ___ \        |\  _____\\  \|\  \|\   ____\|\  \|\  \ |\  ___ \ |\   __  \    ",
        @"\ \  \    \ \  \ \  \|\  \ \  \|\  \ \  \_|\ \       \ \  \__/\ \  \\\  \ \  \___|\ \  \/  /|\ \   __/|\ \  \|\  \   ",
        @" \ \  \  __\ \  \ \  \\\  \ \   _  _\ \  \ \\ \       \ \   __\\ \  \\\  \ \  \    \ \   ___  \ \  \_|/_\ \   _  _\  ",
        @"  \ \  \|\__\_\  \ \  \\\  \ \  \\  \\ \  \_\\ \       \ \  \_| \ \  \\\  \ \  \____\ \  \\ \  \ \  \_|\ \ \  \\  \| ",
        @"   \ \____________\ \_______\ \__\\ _\\ \_______\       \ \__\   \ \_______\ \_______\ \__\\ \__\ \_______\ \__\\ _\ ",
        @"    \|____________|\|_______|\|__|\|__|\|_______|        \|__|    \|_______|\|_______|\|__| \|__|\|_______|\|__|\|__|",
    ];

    public static void Main()
    {
        PrintBanner();

        Write($"Word Fucker ver. {Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "unknown"}", ConsoleColor.Gray);

        Header("Loading Dictionary");

        Definitions = WordDefinitions.LoadFromFile(new FileInfo(Path.Join(Directory.GetCurrentDirectory(), Dictionary)), 16);

        Write($"{Definitions.WordCount} words loaded.", ConsoleColor.Green);

        do
        {
            Header("Pick A Game");

            switch (Selector("Pick a game:", ["Word Hunt", "Anagrams"]))
            {
                case 0:
                    Header("Word Hunt");

                    WordHuntGrid grid = new WordHuntGrid(Definitions, HuntGrid("Input grid:"));

                    Write("Finding words...", ConsoleColor.Cyan);

                    List<string> words = [.. grid.FindAllWords().OrderBy(s => s.Length)];

                    Header("Results");

                    Write($"{words.Count} words found.", ConsoleColor.Green);

                    foreach (string word in words)
                        Write(word, ConsoleColor.White);

                    break;

                case 1:

                    break;
            }
        }
        while (TrueFalse("Play again?"));

        Write("Thank you for using WordFucker!", ConsoleColor.Magenta);
    }


    public static void PrintBanner()
    {
        ConsoleColor[] gradient = [
            ConsoleColor.Cyan, ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.Blue,
            ConsoleColor.Magenta, ConsoleColor.Magenta, ConsoleColor.DarkMagenta];

        for (int i = 0; i < Banner.Length; i++)
        {
            Console.ForegroundColor = gradient[i % gradient.Length];
            Console.WriteLine(Banner[i]);
        }

        Console.ResetColor();
        Console.WriteLine();
    }


    public static void Header(string text, ConsoleColor color = ConsoleColor.Yellow)
    {
        Console.ForegroundColor = color;

        string line = new string('-', text.Length + 4);

        Console.WriteLine(line);
        Console.WriteLine($"| {text} |");
        Console.WriteLine(line);

        Console.ResetColor();
    }


    public static void Write(string text, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
    }


    public static bool TrueFalse(string prompt, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = color;

        while (true)
        {
            Console.Write($"{prompt} (y/n): ");

            int c = Console.Read();
            if (c == 'y' || c == 'n')
                return c == 'y';

            Console.WriteLine($"Invalid input: {c}");
        }
    }


    public static int Selector(string prompt, string[] values, ConsoleColor color = ConsoleColor.White)
    {
        return Selector(prompt, values.Select(x => (x, (char?)null)).ToArray(), color);
    }


    public static int Selector(string prompt, (string values, char? customOptions)[] values, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = color;

        while (true)
        {
            Console.WriteLine(prompt);

            for (int i = 0; i < values.Length; i++)
            {
                (string value, char? customOption) = values[i];
                customOption ??= i.ToString()[0];
                values[i].customOptions = customOption;

                Console.WriteLine($"{customOption}: {value}");
            }

            Console.Write("Select an option: ");

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.KeyChar < 32 || key.KeyChar > 126)
                    continue;

                char c = key.KeyChar;

                int index = Array.FindIndex(
                    values,
                    x => x.customOptions == c);

                if (index >= 0)
                {
                    Console.WriteLine();
                    return index;
                }

                Console.WriteLine();
                Console.WriteLine(
                    $"Invalid input: {c}. Must be any of: " +
                    $"[{string.Join(',', values.Select(x => x.customOptions))}]");

                break;
            }
        }
    }


    public static char[,] HuntGrid(string prompt, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(prompt);

        char[,] huntGridChars = new char[4, 4];

        Console.WriteLine("+---+---+---+---+");

        int position = 0;

        while (position < 16)
        {
            int x = position % 4;
            int y = position / 4;

            Console.Write("| ");

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (position > 0)
                    {
                        position--;

                        // Move to the previous cell visually.
                        Console.Write("\b\b   \b\b\b");

                        int previousX = position % 4;
                        int previousY = position / 4;

                        // If we crossed a row boundary, move up.
                        if (previousX == 3)
                        {
                            Console.SetCursorPosition(
                                Console.CursorLeft,
                                Console.CursorTop - 2);
                        }
                    }

                    continue;
                }

                char c = key.KeyChar;

                // Printable ASCII only.
                if (c < 32 || c > 126)
                    continue;

                huntGridChars[x, y] = c;
                Console.Write($"{c} ");

                position++;
                break;
            }

            // End of row.
            if (position % 4 == 0 && position < 16)
            {
                Console.WriteLine("|");
                Console.WriteLine("+---+---+---+---+");
            }
        }

        Console.WriteLine("|");
        Console.WriteLine("+---+---+---+---+");

        return huntGridChars;
    }
}
