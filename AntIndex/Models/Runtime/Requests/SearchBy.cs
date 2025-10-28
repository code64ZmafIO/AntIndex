using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа по найденным родителям (ByKey)
/// </summary>
/// <param name="entityType">Целевой тип сущности</param>
/// <param name="parentType">Тип сущности родителя (ByKey)</param>
/// <param name="filter">Фильтр результатов</param>
public class SearchBy(
    byte entityType,
    byte parentType,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? filter = null) : AntRequest(entityType, filter)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual IEnumerable<Key> SelectParents(AntRequest parentRequest)
        => parentRequest.SearchResult.Keys;

    public override void ProcessRequest(
        IndexInstance index,
        SearchContextBase searchContext,
        Dictionary<int, ushort>[] wordsBundle,
        CancellationToken ct)
    {
        if (!index.Entities.TryGetValue(EntityType, out var entities)
            || !(searchContext.GetRequestByType(parentType) is { } byStrat))
            return;

        var searchedEntities = new Dictionary<Key, EntityMatchesBundle>();

        for (int queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            var ck = searchContext.Perfomance.GetPerfomancer(wordsBundle[queryWordPosition].Count);

            foreach (var indexWordInfo in wordsBundle[queryWordPosition])
            {
                if (!ck.NeedContinue)
                    break;

                ck.IncrementCheck();

                int wordId = indexWordInfo.Key;

                bool isMatchedWord = false;
                foreach (var wordMatchMeta in index.EntitiesByWordsIndex.GetMatchesByWordAndParents(
                    wordId,
                    EntityType,
                    SelectParents(byStrat)))
                {
                    if (ct.IsCancellationRequested)
                        return;

                    isMatchedWord = true;

                    EntityMeta entityMeta = entities[wordMatchMeta.EntityId];
                    Key entityKey = entityMeta.Key;

                    ref var entityMatch = ref CollectionsMarshal.GetValueRefOrAddDefault(searchedEntities, entityKey, out var exists);

                    if (!exists)
                        entityMatch = new(entityMeta);

                    entityMatch!.AddMatch(
                        new(queryWordPosition,
                            wordMatchMeta,
                            indexWordInfo.Value));
                }

                if (isMatchedWord)
                    ck.IncrementMatch();
            }
        }

        SearchResult = searchedEntities;
    }
}
