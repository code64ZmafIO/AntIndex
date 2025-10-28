using System.Runtime.InteropServices;
using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа
/// </summary>
/// <param name="entityType">Целевой тип сущности</param>
/// <param name="filter">Фильтр результатов</param>
/// <param name="forceSelect">Принудительное получение сущностей целеовго типа по id</param>
/// <param name="onlyForced">Не производит поиск по индексу</param>
public class Search(
    byte entityType,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? filter = null,
    IEnumerable<int>? forceSelect = null,
    bool onlyForced = false)
    : AntRequest(entityType, filter)
{
    public override void ProcessRequest(
        IndexInstance index,
        SearchContextBase searchContext,
        Dictionary<int, ushort>[] wordsBundle,
        CancellationToken ct)
    {
        if (!index.Entities.TryGetValue(EntityType, out var entities))
            return;

        var searchedEntities = new Dictionary<Key, EntityMatchesBundle>();

        foreach (int id in forceSelect ?? [])
        {
            if (entities.TryGetValue(id, out EntityMeta? meta))
                searchedEntities.TryAdd(meta.Key, new(meta));
        }

        if (onlyForced && forceSelect is not null)
        {
            SearchResult = searchedEntities;
            return;
        }

        for (int queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            var ck = searchContext.Perfomance.GetPerfomancer(wordsBundle[queryWordPosition].Count);

            foreach (var indexWordInfo in wordsBundle[queryWordPosition])
            {
                if (!ck.NeedContinue)
                    break;

                ck.IncrementCheck();

                int wordId = indexWordInfo.Key;

                List<WordMatchMeta>? list = index.EntitiesByWordsIndex.GetMatchesByWord(wordId, EntityType);

                if (list is null)
                    continue;

                ck.IncrementMatch();

                for (int i = 0; i < list.Count; i++)
                {
                    if (ct.IsCancellationRequested)
                        return;

                    WordMatchMeta? wordMatchMeta = list[i];
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
            }
        }

        SearchResult = searchedEntities;
    }
}
