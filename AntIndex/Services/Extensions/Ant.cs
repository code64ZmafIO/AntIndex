using System;
using System.Runtime;
using System.Runtime.InteropServices;
using AntIndex.Interfaces;
using AntIndex.Models;
using AntIndex.Models.Index;
using AntIndex.Models.Runtime;
using AntIndex.Services.Normalizing;
using AntIndex.Services.Splitting;
using ProtoBuf;

namespace AntIndex.Services.Extensions;

public static class Ant
{
    #region Tools
    public static Phrase Phrase<TPhraseType>(string phrase, TPhraseType phraseType) where TPhraseType : Enum
        => new(phrase, Convert.ToByte(phraseType));

    public static Phrase Phrase(string phrase, byte phraseType)
        => new(phrase, phraseType);

    public static Phrase Phrase(string phrase)
        => new(phrase, 0);

    public static Key Key<TType>(TType type, int id) where TType : Enum
        => new(Convert.ToByte(type), id);

    public static Key Key(byte type, int id)
        => new(type, id);

    public static Key[] Keys<TType>(TType type, params int[] ids) where TType : Enum
        => Array.ConvertAll(ids, id => Key(type, id));

    public static Key[] Keys(byte type, params int[] ids)
        => Array.ConvertAll(ids, id => Key(type, id));

    public const short NGRAM_LENGTH = 2;

    public static int[] GetNgrams(string word)
    {
        var normalized = $" {word} ";

        int[] result = new int[word.Length + 1];

        for (var i = 0; i <= normalized.Length - NGRAM_LENGTH; i++)
        {
            var nGramm = normalized.AsSpan(i, NGRAM_LENGTH);
            result[i] = GetNGrammHash(nGramm);
        }

        return result;
    }

    private static int GetNGrammHash(in ReadOnlySpan<char> value)
    {
        int num = 5381;
        int num2 = num;
        for (int i = 0; i < value.Length; i += 2)
        {
            num = (num << 5) + num ^ value[i];

            if (i + 1 < value.Length)
                num2 = (num2 << 5) + num2 ^ value[i + 1];
        }
        return num + num2 * 1566083941;
    }
    #endregion

    #region Build
    public static IndexInstance Build(
        INormalizer normalizer,
        IPhraseSplitter phraseSplitter,
        IEnumerable<IIndexedEntity> indexedEntities)
    {
        var entities = new Dictionary<byte, Dictionary<int, EntityMeta>>();
        var entitiesByWordsIndex = new EntitiesByWordsIndex();
        var wordsBundle = new WordsBuildBundle();
        var childs = new Dictionary<Key, HashSet<Key>>();

        foreach (var indexedEntity in indexedEntities)
        {
            Key key = indexedEntity.GetKey();

            if (entities.TryGetValue(key.Type, out var ids) && ids.ContainsKey(key.Id))
                continue;

            var names = indexedEntity.GetNames();
            var chains = indexedEntity.ChainedKeys();
            var byKeys = indexedEntity.ByKeys();
            HashSet<Key> nodesKeys = [];

            for (var i = 0; i < byKeys.Length; i++)
            {
                nodesKeys.Add(byKeys[i]);
            }

            foreach (var node in indexedEntity.ChainedKeys())
            {
                nodesKeys.Add(node);

                ref var set = ref CollectionsMarshal.GetValueRefOrAddDefault(childs, node, out var exists);

                if (!exists)
                    set = [];

                set!.Add(key);
            }

            HashSet<int> uniqWords = [];
            (string[] TokenizedPhrase, byte PhraseType)[] namesToBuild = GetNamesToBuild(names, normalizer, phraseSplitter);
            for (int nameIndex = 0; nameIndex < namesToBuild.Length; nameIndex++)
            {
                (string[] phrase, byte phraseType) = namesToBuild[nameIndex];

                for (byte wordNamePosition = 0; wordNamePosition < phrase.Length && wordNamePosition < byte.MaxValue; wordNamePosition++)
                {
                    string word = phrase[wordNamePosition];
                    var wordId = wordsBundle.GetWordId(word);

                    if (!uniqWords.Add(wordId))
                        continue;

                    WordMatchMeta wordMatchMeta = new(key.Id, wordNamePosition, phraseType);
                    entitiesByWordsIndex.AddMatch(wordId, key.Type, byKeys, wordMatchMeta);
                }
            }

            ref var byTypeEntiteies = ref CollectionsMarshal.GetValueRefOrAddDefault(entities, key.Type, out var containsType);

            if (!containsType)
                byTypeEntiteies = [];

            byTypeEntiteies![key.Id] = new(key, [.. nodesKeys]);
        }

        foreach (var item in childs)
            entities[item.Key.Type][item.Key.Id].Childs = [.. item.Value];

        Dictionary<int, HashSet<int>> wordsIdsByNgramms = [];
        Dictionary<int, int[]> wordsByIds = [];

        foreach (var item in wordsBundle.GetWordsByIds())
        {
            int[] ngramms = GetNgrams(item.Key);

            wordsByIds[item.Value] = ngramms;

            for (int i = 0; i < ngramms.Length; i++)
            {
                int ngramm = ngramms[i];
                ref var words = ref CollectionsMarshal.GetValueRefOrAddDefault(wordsIdsByNgramms, ngramm, out var exists);

                if (!exists)
                    words = [];

                words!.Add(item.Value);
            }
        }

        return new IndexInstance()
        {
            Entities = entities,
            WordsByIds = wordsByIds,
            EntitiesByWordsIndex = entitiesByWordsIndex,
            WordsIdsByNgramms = wordsIdsByNgramms.ToDictionary(i => i.Key, i => i.Value.ToArray()),
        };
    }

    private static (string[] TokenizedPhrase, byte PhraseType)[] GetNamesToBuild(
        IEnumerable<Phrase> phrases,
        INormalizer normalizer,
        IPhraseSplitter phraseSplitter)
        => [.. phrases.Select(phrase =>
        {
            string normalizedPhrase = normalizer.Normalize(phrase.Text!);
            string[] tokenizedPhrase = phraseSplitter.Tokenize(normalizedPhrase);
            return (tokenizedPhrase, phrase.PhraseType);
        })];

    private class WordsBuildBundle()
    {
        private int CurrentId = int.MinValue;

        private readonly Dictionary<string, int> Pairs = [];

        public int GetWordId(string word)
        {
            ref var id = ref CollectionsMarshal.GetValueRefOrAddDefault(Pairs, word, out var exists);
            if (exists)
                return id;

            id = CurrentId++;
            return id;
        }

        public IEnumerable<KeyValuePair<string, int>> GetWordsByIds()
        {
            var words = new Dictionary<int, Word>(Pairs.Count);

            foreach (var wordIdPair in Pairs.OrderBy(i => i.Key))
                yield return wordIdPair;
        }
    }
    #endregion

    #region Serialization
    public static void WriteIndex(IndexInstance index, string filePath)
        => WriteObject(filePath, index);

    public static IndexInstance ReadIndex(string filePath)
    {
        IndexInstance index = ReadAndDeserializeObject<IndexInstance>(filePath);
        index.Trim();

        return index;
    }

    public static T ReadAndDeserializeObject<T>(string filePath) where T : class
    {
        using Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Serializer.Deserialize<T>(stream);
    }

    public static void WriteObject(string filePath, object obj)
    {
        string? directoryPath = Path.GetDirectoryName(filePath);

        if (directoryPath is null)
            return;

        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        using Stream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

        Serializer.Serialize(stream, obj);
    }
    #endregion
}

public record struct Phrase(string Text, byte PhraseType);
