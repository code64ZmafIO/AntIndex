using System.Data;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AntIndex.Models.Abstract;
using AntIndex.Models.Index;
using AntIndex.Models.Runtime;
using AntIndex.Models.Runtime.Requests;
using ProtoBuf;

namespace AntIndex.Models;

[ProtoContract]
public class IndexInstance
{
    public IndexInstance() { }

    [ProtoMember(1)]
    public Dictionary<byte /*TypeId*/, Dictionary<int /*EntityId*/, EntityMeta>> Entities { get; set; } = [];

    [ProtoMember(2)]
    public Dictionary<int /*NGrammHash*/, int[] /*WordsIds*/> WordsIdsByNgramms { get; set; } = [];

    [ProtoMember(3)]
    public Dictionary<int /*WordId*/, int[] /*NGrammHashes*/> WordsByIds { get; set; } = [];

    [ProtoMember(4)]
    public EntitiesByWordsIndex EntitiesByWordsIndex { get; set; } = new();

    public int EntitesCount => Entities.Sum(i => i.Value.Count);

    #region Search
    public List<TypeSearchResult> SearchTypes<TContext>(TContext searchContext, (byte Type, int Take)[] selectTypes, CancellationToken? cancellationToken = null)
        where TContext : SearchContextBase
    {
        SearchInternal(searchContext, cancellationToken);

        var result = new List<TypeSearchResult>();

        foreach ((short Type, int Take) in selectTypes)
        {
            AntRequest? request = Array.Find(searchContext.Request, i => i is not AppendChilds && i.EntityType == Type);

            if (request is null)
                continue;

            var typeResult = searchContext
                .PostProcessing(request
                    .GetResults()
                    .OrderByDescending(i =>
                    {
                        i.Score = CalculateScore(i, searchContext);
                        return i.Score;
                    })
                )
                .Take(Take)
                .ToList();

            result.Add(new(request.EntityType, typeResult));
        }

        return result;
    }

    public List<EntityMatchesBundle> Search<TContext>(TContext searchContext, int take = 30, CancellationToken? cancellationToken = null)
        where TContext : SearchContextBase
    {
        SearchInternal(searchContext, cancellationToken);

        return searchContext.PostProcessing(GetAllResults()
            .OrderByDescending(i =>
            {
                i.Score = CalculateScore(i, searchContext);
                return i.Score;
            }))
            .Take(take)
            .ToList();

        IEnumerable<EntityMatchesBundle> GetAllResults()
        {
            foreach (var request in searchContext.Request)
            {
                foreach (var item in request.GetResults())
                    yield return item;
            }
        }
    }

    private void SearchInternal<TContext>(TContext searchContext, CancellationToken? cancellationToken = null)
        where TContext : SearchContextBase
    {
        var ct = cancellationToken ?? new CancellationTokenSource(searchContext.TimeoutMs).Token;

        Dictionary<int, ushort>[] wordsBundle = SearchSimlarIndexWordsByQuery(searchContext);

        foreach (var i in searchContext.Request)
            i.ProcessRequest(this, searchContext, wordsBundle, ct);
    }

    private Dictionary<int, ushort>[] SearchSimlarIndexWordsByQuery<TContext>(TContext searchContext)
        where TContext : SearchContextBase
    {
        var result = new Dictionary<int, ushort>[searchContext.SplittedQuery.Length];

        for (int i = 0; i < result.Length; i++)
        {
            QueryWordContainer currentWord = searchContext.SplittedQuery[i];

            //Проверка на введеное слово ранее, чтобне повторять вычисления
            for (int j = i - 1; j >= 0; j--)
            {
                if (searchContext.SplittedQuery[j].QueryWord.Equals(currentWord.QueryWord))
                {
                    result[i] = result[j];
                    break;
                }
            }

            if (result[i] is null)
            {
                result[i] = SearchSimilarWordByQueryAndAlternatives(
                    currentWord,
                    searchContext.SimilarityTreshold,
                    searchContext.Perfomance.MaxCheckingCount);
            }
        }

        return result;
    }

    private Dictionary<int, ushort> SearchSimilarWordByQueryAndAlternatives(
        QueryWordContainer wordContainer,
        double similarityTreshold,
        int maxBundleLength)
    {
        Dictionary<int, ushort>? result = null;

        SearchSimilars(wordContainer.QueryWord, false);

        for (int i = 0; i < wordContainer.Alternatives.Length; i++)
            SearchSimilars(wordContainer.Alternatives[i], true);

        return result ?? [];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SearchSimilars(Word queryWord, bool isAlterantive)
        {
            int treshold;
            if (isAlterantive)
                treshold = 1;
            else
                treshold = queryWord.IsDigit
                    ? queryWord.NGrammsHashes.Length - 1
                    : (int)(wordContainer.QueryWord.NGrammsHashes.Length * similarityTreshold);

            var similars = GetSimilarWords(queryWord, treshold);

            result ??= new Dictionary<int, ushort>(similars.Count);

            foreach (KeyValuePair<int, ushort> item in similars
                .Where(i => ValidateWord(queryWord, i, treshold))
                .OrderByDescending(i => i.Value))
            {
                if (result.Count > maxBundleLength)
                    return;

                ref var matchInfo = ref CollectionsMarshal.GetValueRefOrAddDefault(result, item.Key, out var exists);

                if (!exists || item.Value > matchInfo)
                    matchInfo = item.Value;
            }
        }
    }

    private bool ValidateWord(Word queryWord, KeyValuePair<int, ushort> indexWordMathes, int treshold)
    {
        if (indexWordMathes.Value < treshold)
            return false;

        const int MaxDistance = 2;

        int[] indexWordHashes = WordsByIds[indexWordMathes.Key];
        int[] queryWordHashes = queryWord.NGrammsHashes;

        int matchesCounter = 0;
        int previousMatchPosition = 0;
        for (var i = 0; i < indexWordHashes.Length; i++)
        {
            for (var j = previousMatchPosition; j < queryWordHashes.Length; j++)
            {
                if (indexWordHashes[i] == queryWordHashes[j])
                {
                    if (j - previousMatchPosition > MaxDistance)
                        return false;

                    matchesCounter++;
                    previousMatchPosition = i;

                    if (matchesCounter == indexWordMathes.Value)
                        return true;

                    break;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Метод отвечает за поиск похожих слов по 2-gramm
    /// </summary>
    /// <param name="queryWord"></param>
    /// <param name="treshold"></param>
    /// <returns>Словарь id слова, количество ngramm</returns>
    private Dictionary<int, ushort> GetSimilarWords(Word queryWord, int treshold)
    {
        var wordLength = queryWord.NGrammsHashes.Length;

        //Считаем количество совпавших ngramm для каждого слова
        Dictionary<int, ushort> words = new(400_000);

        for (int queryWordNgrammIndex = 0; queryWordNgrammIndex < wordLength; queryWordNgrammIndex++)
        {
            if (!WordsIdsByNgramms.TryGetValue(queryWord.NGrammsHashes[queryWordNgrammIndex], out var wordsIds))
                continue;

            for (int i = 0; i < wordsIds.Length; i++)
            {
                int wordId = wordsIds[i];

                ref var matchInfo = ref CollectionsMarshal.GetValueRefOrNullRef(words, wordId);

                if (!Unsafe.IsNullRef(ref matchInfo))
                    matchInfo++;
                else if (queryWordNgrammIndex == 0 || (!queryWord.IsDigit && queryWordNgrammIndex <= treshold))
                    words[wordId] = 1;
            }
        }

        return words;
    }

    private static int CalculateScore(
        EntityMatchesBundle entityMatchesBundle,
        SearchContextBase searchContext)
    {
        Span<int> wordsScores = stackalloc int[searchContext.SplittedQuery.Length];

        //Считаем основные совпадения
        CalculateNodeMatchesScore(in wordsScores, searchContext, entityMatchesBundle.WordsMatches, 1);

        //Считаем совпадения в связанных нодах
        Key[] nodes = entityMatchesBundle.EntityMeta.Nodes;
        for (int i = 0; i < nodes.Length; i++)
        {
            Key nodeKey = nodes[i];

            if (searchContext.GetRequestByType(nodeKey.Type) is { } req
                && req.SearchResult.TryGetValue(nodeKey, out var chaiedMathes))
            {
                double nodeMultipler = searchContext.GetEntityNodeMiltipler(entityMatchesBundle.Key.Type, nodeKey.Type);
                CalculateNodeMatchesScore(in wordsScores, searchContext, chaiedMathes.WordsMatches, nodeMultipler);

                if (searchContext.OnChainedNodeMatched(entityMatchesBundle.Key, nodeKey) is { } chainedMatchRule)
                    entityMatchesBundle.Rules.Add(chainedMatchRule);
            }
        }

        var resultScore = 0;
        for (int i = 0; i < wordsScores.Length; i++)
            resultScore += wordsScores[i];

        if (searchContext.OnEntityProcessed(entityMatchesBundle) is { } rule)
            entityMatchesBundle.Rules.Add(rule);

        return resultScore;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateNodeMatchesScore(
        in Span<int> wordsScores,
        SearchContextBase searchContext,
        List<WordCompareResult> wordsMatches,
        double nodeMultipler)
    {
        //TODO: тут надо хорошо подумать как дистинктить слова и находить целое слово написанное раздельно
        for (int wordMatchIndex = 0; wordMatchIndex < wordsMatches.Count; wordMatchIndex++)
        {
            WordCompareResult compareResult = wordsMatches[wordMatchIndex];
            WordMatchMeta matchMeta = compareResult.MatchMeta;

            int score = compareResult.MatchLength;

            int queryWordPosition = compareResult.QueryWordPosition;
            double phraseMultipler = searchContext.GetPhraseMultiplerInternal(matchMeta.PhraseType);

            score = (int)(score * phraseMultipler * nodeMultipler);

            if (wordsScores[queryWordPosition] < score)
                wordsScores[queryWordPosition] = score;
        }
    }
    #endregion

    public void Trim()
    {
        Key GetKey(Key key)
            => Entities[key.Type][key.Id].Key;

        foreach (var collection in Entities.Values)
        {
            foreach (var meta in collection.Values)
            {
                if (meta.Nodes.Length == 0)
                    meta.Nodes = Array.Empty<Key>();
                else
                {
                    for (int i = 0; i < meta.Nodes.Length; i++)
                        meta.Nodes[i] = GetKey(meta.Nodes[i]);
                }

                if (meta.Childs.Length == 0)
                    meta.Childs = Array.Empty<Key>();
                else
                {
                    for (int i = 0; i < meta.Childs.Length; i++)
                        meta.Childs[i] = GetKey(meta.Childs[i]);
                }
            }

            collection.TrimExcess();
        }

        Entities.TrimExcess();
        WordsByIds.TrimExcess();
        WordsIdsByNgramms.TrimExcess();
        EntitiesByWordsIndex.Trim(GetKey);


        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
    }
}