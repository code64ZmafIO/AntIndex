using ProtoBuf;

namespace AntIndex.Models.Index;

[ProtoContract]
public class EntityMeta
{
    public EntityMeta() { }

    public EntityMeta(Key key, Key[] nodes)
    {
        Key = key;
        Nodes = nodes;
    }

    [ProtoMember(1)]
    public Key Key { get; } = new(0, 0);

    [ProtoMember(2)]
    public Key[] Nodes { get; set; } = Array.Empty<Key>();

    [ProtoMember(3)]
    public Key[] Childs { get; set; } = Array.Empty<Key>();
}
