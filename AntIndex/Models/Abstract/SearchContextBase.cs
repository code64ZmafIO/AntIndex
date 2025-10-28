using AntIndex.Models.Index;
using AntIndex.Models.Runtime;
using AntIndex.Models.Runtime.Requests;
using AntIndex.Services.Normalizing;
using AntIndex.Services.Splitting;

namespace AntIndex.Models.Abstract;

public abstract class SearchContextBase(
    string query,
    INormalizer normalizer,
    IPhraseSplitter splitter)
{
    public string Query { get; } = normalizer.Normalize(query);

    public abstract AntRequest[] Request { get; }


    private QueryWordContainer[]? _splittedQuery;

    public QueryWordContainer[] SplittedQuery
        => _splittedQuery ??= GetSplittedQuery();

    private QueryWordContainer[] GetSplittedQuery()
        => Array.ConvertAll(splitter.Tokenize(Query),
            word =>
            {
                bool notRealivated = NotRealivatedWords.Contains(word);

                Word[] alterantivesMetas = Array.Empty<Word>();

                if (AlternativeWords.TryGetValue(word, out var alternatives))
                    alterantivesMetas = Array.ConvertAll(alternatives, alt => new Word(alt));

                return new QueryWordContainer(
                    new Word(word),
                    alterantivesMetas,
                    notRealivated);
            });

    public virtual IOrderedEnumerable<EntityMatchesBundle> PostProcessing(IOrderedEnumerable<EntityMatchesBundle> result)
        => result;

    public virtual double GetEntityNodeMiltipler(byte entityType, byte chainedNodeType)
        => 1;

    public virtual double GetPhraseTypeMultipler(byte phraseType)
        => 1;

    public virtual RuleScore? OnChainedNodeMatched(Key entityKey, Key chainedKey)
        => null;

    public virtual RuleScore? OnEntityProcessed(EntityMatchesBundle entityMatchsBundle)
        => null;

    public virtual int TimeoutMs
        => 1500;

    public virtual bool IsSimpleQuery
        => SplittedQuery.Length < 2;

    public virtual double SimilarityTreshold
        => 0.7;

    public virtual HashSet<string> NotRealivatedWords { get; } = [];

    public virtual Dictionary<string, string[]> AlternativeWords { get; } = [];

    public virtual PerfomanceSettings Perfomance
        => SplittedQuery.Length > 5
            ? PerfomanceSettings.Fast
            : PerfomanceSettings.Default;

    internal double GetPhraseMultiplerInternal(byte phraseType)
    {
        if (phraseType == 0)
            return 1;

        return GetPhraseTypeMultipler(phraseType);
    }

    internal AntRequest? GetRequestByType(byte type)
    {
        for (int i = 0; i < Request.Length; i++)
        {
            AntRequest? request = Request[i];

            if (request is AppendChilds)
                continue;

            if (request.EntityType == type)
                return request;
        }

        return null;
    }
}

public record QueryWordContainer(Word QueryWord, Word[] Alternatives, bool NotRealivated);
