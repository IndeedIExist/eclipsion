using System;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Audio.CustomBoombox;
public sealed class MsgCustomBoomboxUpload : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableOrdered;

    public NetEntity Boombox;

    public byte[] Data = Array.Empty<byte>();

    public string FileName = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Boombox = buffer.ReadNetEntity();
        var len = buffer.ReadVariableInt32();
        Data = buffer.ReadBytes(len);
        FileName = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Boombox);
        buffer.WriteVariableInt32(Data.Length);
        buffer.Write(Data);
        buffer.Write(FileName);
    }
}
