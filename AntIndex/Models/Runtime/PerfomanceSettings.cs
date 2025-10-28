namespace AntIndex.Models.Runtime;

public record PerfomanceSettings(
    int MaxCheckingCount,
    int SearchedWordsCount,
    int MinCheckingCount,
    double CheckingPrecent)
{
    public static readonly PerfomanceSettings Default = new(5000, 4, 50, 0.1);

    public static readonly PerfomanceSettings Fast = new(500, 2, 20, 0.07);

    public Perfomancer GetPerfomancer(int similarWordsCount)
    {
        var maxCheckingWords = Math.Max(MinCheckingCount, (int)(similarWordsCount * CheckingPrecent));

        return new(maxCheckingWords, SearchedWordsCount);
    }
}
