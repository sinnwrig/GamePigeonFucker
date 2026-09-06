using System;
using System.IO;


public sealed class TrieNode
{
    public readonly TrieNode?[] Children = new TrieNode?[26];
    public bool IsWord;
}


public class WordDefinitions
{
    private List<string> _words;
    public int WordCount => _words.Count;
    public TrieNode Root { get; }


    public WordDefinitions(List<string> words)
    {
        _words = words;
        Root = BuildTrie(words);
    }


    public static WordDefinitions LoadFromFile(FileInfo file, int wordLengthCap)
    {
        return LoadFromLines(File.ReadAllLines(file.FullName), wordLengthCap);
    }


    public static WordDefinitions LoadFromPlaintext(string plaintext, int wordLengthCap)
    {
        return LoadFromLines(plaintext.Split(["\n", "\r\n"], StringSplitOptions.TrimEntries), wordLengthCap);
    }


    public static WordDefinitions LoadFromLines(string[] lines, int wordLengthCap)
    {
        List<string> words = [];

        for (int i = 0; i < lines.Length; i++)
        {
            if (IsValidWord(lines[i], wordLengthCap))
                words.Add(lines[i].ToUpper());
        }

        return new WordDefinitions(words);
    }


    private static bool IsValidWord(string word, int wordLengthCap)
    {
        string trim = word.Trim();
        return trim.Length <= wordLengthCap && trim.All(char.IsAsciiLetterUpper);
    }


    private static TrieNode BuildTrie(List<string> words)
    {
        TrieNode root = new();

        foreach (string word in words)
        {
            TrieNode node = root;

            foreach (char c in word)
            {
                int index = c - 'A';
                node = node.Children[index] ??= new TrieNode();
            }

            node.IsWord = true;
        }

        return root;
    }
}
