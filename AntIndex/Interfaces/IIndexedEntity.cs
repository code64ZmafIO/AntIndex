using AntIndex.Models.Index;
using AntIndex.Services.Extensions;

namespace AntIndex.Interfaces;

public interface IIndexedEntity
{
    Key GetKey();

    IEnumerable<Phrase> GetNames();

    IEnumerable<Key> ChainedKeys();

    Key[] ByKeys() => Array.Empty<Key>();
}
