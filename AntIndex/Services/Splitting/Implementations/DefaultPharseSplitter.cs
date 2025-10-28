using System.Text;

namespace AntIndex.Services.Splitting.Implementations;

public class DefaultPharseSplitter : IPhraseSplitter
{
    public static readonly DefaultPharseSplitter Instance = new();

    private static readonly char[] _splitChars =
    {
        ' ',
        '#',
        '№',
        ')',
        '(',
        '.',
        ',',
        '^',
        '\''
    };

    /// <summary>
    /// Производит разбиение строки на токены
    /// </summary>
    public string[] Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        return value.Normalize(NormalizationForm.FormC)
                    .Split(_splitChars, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .SelectMany(TokenSplit)
                    .ToArray();
    }

    private static IEnumerable<string> TokenSplit(string word)
    {
        foreach (var preSplit in word.Split('-',
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var i in SearchAllDigitWordCombinations(preSplit)) yield return i;
        }
    }

    private static IEnumerable<string> SearchAllDigitWordCombinations(string value)
    {
        var combIndex = GetWordDigitIndex(value);

        if (combIndex != -1)
        {
            combIndex++;
            yield return value[..combIndex];
            foreach (var i in SearchAllDigitWordCombinations(value[combIndex..])) yield return i;
        }
        else
        {
            yield return value;
        }
    }

    private static int GetWordDigitIndex(string value)
    {
        for (var i = 0; i < value.Length - 1; i++)
        {
            var currentSymbol = value[i];
            var nextSymbol = value[i + 1];

            if (!char.IsDigit(currentSymbol) && char.IsDigit(nextSymbol)) return i;
            if (char.IsDigit(currentSymbol) && !char.IsDigit(nextSymbol)) return i;

            if (char.IsLetterOrDigit(currentSymbol) && char.IsPunctuation(nextSymbol)) return i;
            if (char.IsPunctuation(currentSymbol) && char.IsLetterOrDigit(nextSymbol)) return i;
        }

        return -1;
    }
}
