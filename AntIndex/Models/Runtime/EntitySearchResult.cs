using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime;

public class TypeSearchResult(byte type, List<EntityMatchesBundle> result)
{
    public byte Type { get; } = type;

    public List<EntityMatchesBundle> Result { get; } = result;
}

public class EntityMatchesBundle(EntityMeta entityMeta)
{
    public EntityMeta EntityMeta { get; } = entityMeta;

    public List<WordCompareResult> WordsMatches { get; } = new(2);

    public Key Key
        => EntityMeta.Key;

    public int Prescore;

    public int Score;

    public List<RuleScore> Rules { get; } = [];

    internal void AddMatch(WordCompareResult wordCompareResult)
    {
        WordsMatches.Add(wordCompareResult);
        Prescore += wordCompareResult.MatchLength;
    }
}

public record WordCompareResult(
    int QueryWordPosition,
    WordMatchMeta MatchMeta,
    int MatchLength);

public record RuleScore(ushort RuleType, ushort Score);