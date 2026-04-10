using System;
using Unity.Collections;
using Unity.Netcode;

public enum P2PChatEntryType : byte
{
    Normal = 0,
    Narration = 1,
    Divider = 2
}

[Serializable]
public struct P2PChatEntry : INetworkSerializable, IEquatable<P2PChatEntry>
{
    public P2PChatEntryType EntryType;
    public ulong SenderClientId;
    public FixedString512Bytes Sender;
    public FixedString512Bytes Message;

    public P2PChatEntry(P2PChatEntryType entryType, ulong senderClientId, string sender, string message)
    {
        EntryType = entryType;
        SenderClientId = senderClientId;
        Sender = new FixedString512Bytes(sender ?? string.Empty);
        Message = new FixedString512Bytes(message ?? string.Empty);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref EntryType);
        serializer.SerializeValue(ref SenderClientId);
        serializer.SerializeValue(ref Sender);
        serializer.SerializeValue(ref Message);
    }

    public bool Equals(P2PChatEntry other)
    {
        return EntryType == other.EntryType
            && SenderClientId == other.SenderClientId
            && Sender.Equals(other.Sender)
            && Message.Equals(other.Message);
    }
}