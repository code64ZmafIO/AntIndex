using AntIndex.Services.Extensions;

namespace AntIndex.Models.Runtime;

public class Word(string word) : IEquatable<Word>
{
    public readonly string QueryWord = word;

    public readonly int[] NGrammsHashes = Ant.GetNgrams(word);

    public readonly bool IsDigit = int.TryParse(word, out _);

    public bool Equals(Word? other)
    {
        if (ReferenceEquals(null, other)) return false;

        return other.QueryWord.Equals(QueryWord);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Word w)
            return false;

        return Equals(w);
    }

    public override int GetHashCode()
        => NGrammsHashes.Length;
}
