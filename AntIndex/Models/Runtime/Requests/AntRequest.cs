using AntIndex.Models.Abstract;
using AntIndex.Models.Index;

namespace AntIndex.Models.Runtime.Requests;

public abstract class AntRequest(byte entityType, Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? filter = null)
{
    public byte EntityType { get; } = entityType;

    public bool IsVisible { get; set; } = true;

    public Dictionary<Key, EntityMatchesBundle> SearchResult { get; set; } = [];

    public Func<IEnumerable<EntityMatchesBundle>, IEnumerable<EntityMatchesBundle>>? Filter { get; set; } = filter;

    public abstract void ProcessRequest(
        IndexInstance index,
        SearchContextBase searchContext,
        Dictionary<int, ushort>[] wordsBundle,
        CancellationToken ct);

    public IEnumerable<EntityMatchesBundle> GetResults()
        => IsVisible 
            ? Filter?.Invoke(SearchResult.Values) ?? SearchResult.Values
            : [];
}
