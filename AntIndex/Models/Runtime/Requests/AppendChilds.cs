using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

/// <summary>
/// Выполняет принудительное добавление дочерних элементов в выдачу
/// </summary>
/// <param name="parentType">Тип родителя</param>
/// <param name="entityType">Целевой тип</param>
/// <param name="appendFilter">Фильтр для дочерних сущностей КАЖДОГО родителя</param>
/// <param name="fromTop">Топ родителей для добавления дочерних</param>
public class AppendChilds(
    byte parentType,
    byte entityType,
    Func<IEnumerable<Key>, IEnumerable<Key>> appendFilter,
    int fromTop = 0) : AntRequest(entityType)
{
    public override void ProcessRequest(
        IndexInstance index,
        SearchContextBase searchContext,
        Dictionary<int, ushort>[] wordsBundle,
        CancellationToken ct)
    {
        if (searchContext.GetRequestByType(parentType) is { } from
            && searchContext.GetRequestByType(EntityType) is { } to
            && index.Entities.TryGetValue(EntityType, out var entities))
        {
            IEnumerable<Key> GetKeys()
            {
                if (fromTop < 1)
                    return from.SearchResult.Keys;
                else
                    return from.SearchResult
                        .OrderByDescending(i => i.Value.Prescore)
                        .Take(fromTop)
                        .Select(i => i.Key);
            }

            foreach (Key i in GetKeys())
            {
                if (ct.IsCancellationRequested)
                    break;

                var parentEntityChilds = index.Entities[i.Type][i.Id].Childs;

                foreach (var child in appendFilter(parentEntityChilds.Where(i => i.Type == EntityType)))
                {
                    var entityMeta = entities[child.Id];

                    to.SearchResult.TryAdd(child, new(entityMeta));
                }
            }
        }
    }
}
