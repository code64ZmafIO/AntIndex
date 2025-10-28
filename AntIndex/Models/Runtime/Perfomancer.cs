namespace AntIndex.Models.Runtime;

public class Perfomancer(int maxCheckingWords, int quantity)
{
    private int MatchesCount = 0;
    private int CheckedCount = 0;

    public void IncrementMatch()
        => MatchesCount++;

    public void IncrementCheck()
        => CheckedCount++;

    public bool NeedContinue
        => MatchesCount <= quantity && CheckedCount <= maxCheckingWords;
}
