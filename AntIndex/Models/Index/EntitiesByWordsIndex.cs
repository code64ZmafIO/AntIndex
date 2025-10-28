using System.Runtime.InteropServices;
using ProtoBuf;

namespace AntIndex.Models.Index;

[ProtoContract]
public class EntitiesByWordsIndex()
{
    [ProtoMember(1)]
    public Dictionary<int /*WordId*/, Dictionary<byte /*TypeId*/, Dictionary</*ByNodeKey*/ Key, List<WordMatchMeta>>>> EntitiesByWords { get; } = [];

    public List<WordMatchMeta>? GetMatchesByWord(int wordId, byte entityType)
    {
        if (EntitiesByWords.TryGetValue(wordId, out var wordMatches)
            && wordMatches.TryGetValue(entityType, out var mathesBundle)
            && mathesBundle.TryGetValue(Key.Default, out var matches))
        {
            return matches;
        }

        return null;
    }

    public IEnumerable<WordMatchMeta> GetMatchesByWordAndParents(
        int wordId,
        byte entityType,
        IEnumerable<Key> parentKeys)
    {
        if (!EntitiesByWords.TryGetValue(wordId, out var wordMatches)
            || !wordMatches.TryGetValue(entityType, out var mathesBundle))
            yield break;

        foreach (var byKey in parentKeys)
        {
            if (!mathesBundle.TryGetValue(byKey, out var entityMatches))
                continue;

            for (int i = 0; i < entityMatches.Count; i++)
                yield return entityMatches[i];
        }
    }

    public void AddMatch(int wordId, byte entityType, Key[] byKeys, WordMatchMeta wordMatch)
    {
        ref var wordMatches = ref CollectionsMarshal.GetValueRefOrAddDefault(EntitiesByWords, wordId, out var exists);

        if (!exists)
            wordMatches = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(wordMatches!, entityType, out exists);

        if (!exists)
            matchesBundle = [];

        if (byKeys.Length > 0)
        {
            for (int i = 0; i < byKeys.Length; i++)
            {
                ref var matches = ref CollectionsMarshal.GetValueRefOrAddDefault(matchesBundle!, byKeys[i], out exists);

                if (!exists)
                    matches = [];

                matches!.Add(wordMatch);
            }
        }
        else
        {
            ref var matches = ref CollectionsMarshal.GetValueRefOrAddDefault(matchesBundle!, Key.Default, out exists);

            if (!exists)
                matches = [];

            matches!.Add(wordMatch);
        }
    }

    public void Trim(Func<Key, Key> GetKey)
    {
        foreach (var collection in EntitiesByWords.Values)
        {
            foreach (var subCollection in collection)
            {
                Dictionary<Key, List<WordMatchMeta>> res = subCollection.Value.Select(i =>
                {
                    List<WordMatchMeta> mathes = i.Value;
                    mathes.TrimExcess();

                    var key = i.Key.Equals(Key.Default)
                        ? Key.Default
                        : GetKey(i.Key);

                    return new KeyValuePair<Key, List<WordMatchMeta>>(key, mathes);
                })
                .ToDictionary();

                res.TrimExcess();

                collection[subCollection.Key] = res;
            }

            collection.TrimExcess();
        }

        EntitiesByWords.TrimExcess();
    }
}
