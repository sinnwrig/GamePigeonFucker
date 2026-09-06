

public static class ConsolePrompts
{
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
}