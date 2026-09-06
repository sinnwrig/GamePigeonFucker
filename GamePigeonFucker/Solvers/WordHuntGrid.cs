using System;
using System.Collections.Concurrent;


public class WordHuntGrid
{
    private WordDefinitions _definitions;
    private char[,] _grid;


    public WordHuntGrid(WordDefinitions defs, char[,] grid)
    {
        _definitions = defs;
        _grid = grid;

        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                _grid[i, j] = char.ToUpper(_grid[i, j]);
    }


    public HashSet<string> FindAllWords()
    {
        ConcurrentBag<HashSet<string>> partials = [];

        Parallel.For(
            0, 16,
            () => new HashSet<string>(),
            (i, _, localWords) =>
            {
                int x = i % 4;
                int y = i / 4;
                char[] buffer = new char[16];

                SearchTile(x, y, buffer, 0, new VisitMarker(), _definitions.Root, localWords);

                return localWords;
            },
            partials.Add);

        HashSet<string> foundWords = [];

        foreach (HashSet<string> partial in partials)
            foundWords.UnionWith(partial);

        return foundWords;
    }


    private void SearchTile(int x, int y, char[] buffer, int depth, VisitMarker visited, TrieNode node, HashSet<string> foundWords)
    {
        char c = _grid[x, y];
        int index = c - 'A';

        if (index < 0 || index >= 26)
            return;

        TrieNode? child = node.Children[index];
        if (child is null)
            return;

        visited.Mark(x, y);
        buffer[depth] = c;

        if (child.IsWord)
            foundWords.Add(new string(buffer, 0, depth + 1));

        for (int yo = -1; yo <= 1; yo++)
        {
            for (int xo = -1; xo <= 1; xo++)
            {
                int xof = x + xo;
                int yof = y + yo;

                if (xof < 0 || xof >= 4 || yof < 0 || yof >= 4)
                    continue;

                if (visited.Get(xof, yof))
                    continue;

                SearchTile(xof, yof, buffer, depth + 1, visited, child, foundWords);
            }
        }
    }

    private struct VisitMarker
    {
        private ushort _value;

        public void Mark(int x, int y) =>
            _value |= (ushort)(1 << Idx(x, y));

        public readonly bool Get(int x, int y) =>
            (_value & (1 << Idx(x, y))) != 0;

        private static int Idx(int x, int y)
        {
            if (x < 0 || x > 3 || y < 0 || y > 3)
                throw new ArgumentOutOfRangeException();

            return x + y * 4;
        }
    }
}
