﻿using ProtoBuf;

namespace AntIndex.Models.Index;

[ProtoContract]
public class Key : IEquatable<Key>
{
    public static readonly Key Default = new(0, 0);

    public Key()
    {

    }

    public Key(byte type, int id)
    {
        Id = id;
        Type = type;
    }

    [ProtoMember(1)]
    public byte Type { get; }

    [ProtoMember(2)]
    public int Id { get; }

    public bool Equals(Key? other)
    {
        if (ReferenceEquals(other, this))
            return true;

        return Id == other!.Id && Type == other.Type;
    }

    public override bool Equals(object? obj)
    {
        // Используем ReferenceEquals для проверки на null
        return ReferenceEquals(this, obj) || obj is Key key && Equals(key);
    }

    public override int GetHashCode()
    {
        int num = 5381;
        int num2 = num;

        num = (num << 5) + (num ^ Type);
        num2 = (num2 << 5) + (num2 ^ Id);

        return num + num2 * 1566083941;
    }
}
