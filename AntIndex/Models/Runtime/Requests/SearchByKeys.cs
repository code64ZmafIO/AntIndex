using System.Runtime.CompilerServices;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа по заданным родителям (ByKey)
/// </summary>
/// <param name="entityType">Целевой тип сущности</param>
/// <param name="parentKeys">Ключи родителей (ByKey)</param>
/// <param name="filter">Фильтр результатов</param>
public class SearchByKeys(
    byte entityType,
    Key[] parentKeys,
    Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? filter = null)
        : SearchBy(entityType, parentKeys[0].Type, filter)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override IEnumerable<Key> SelectParents(AntRequest context)
        => parentKeys;
}
