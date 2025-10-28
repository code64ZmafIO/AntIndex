using ProtoBuf;

namespace AntIndex.Models.Index;

[ProtoContract]
public class WordMatchMeta
{
    [ProtoMember(1)]
    public int EntityId { get; }

    [ProtoMember(2)]
    public byte NameWordPosition { get; }

    [ProtoMember(3)]
    public byte PhraseType {  get; }

    public WordMatchMeta()
    {
    }

    public WordMatchMeta(
        int entityId,
        byte nameWordPosition,
        byte phraseType)
    {
        EntityId = entityId;
        NameWordPosition = nameWordPosition;
        PhraseType = phraseType; 
    }
}
