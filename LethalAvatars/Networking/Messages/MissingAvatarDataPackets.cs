using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using LethalAvatars.Libs;
using ProtoBuf;

namespace LethalAvatars.Networking.Messages;

[ProtoContract]
internal class MissingAvatarDataPackets : AvatarMessage
{
    public override string MessageName { get; set; } = $"{Plugin.GUID}.MissingAvatarDataPackets";
    [ProtoMember(7)] public List<int> MissingDatas = new();
    [ProtoMember(8)] public string AvatarHash = String.Empty;

    private void GetAvatarDatas(byte[] avatarData, Action<AvatarData[]> onAvatarDatas)
    {
        List<AvatarData> avatarDatas = new();
        Extensions.ChunkData(avatarData, chunkedAvatarData =>
        {
            int[] missingPackets = MissingDatas.ToArray();
            foreach (int missingPacket in missingPackets)
                avatarDatas.Add(new AvatarData
                {
                    Data = chunkedAvatarData.ElementAt(missingPacket),
                    PacketNumber = missingPacket,
                    MaxPacketNumbers = chunkedAvatarData.Count,
                    AvatarHash = Extensions.GetHashOfData(avatarData)
                });
            onAvatarDatas.Invoke(avatarDatas.OrderBy(x => x.PacketNumber).ToArray());
        });
    }
    
    protected override void Handle(PlayerControllerB fromPlayer, AvatarMessage avatarMessage)
    {
        MissingAvatarDataPackets receiving = (MissingAvatarDataPackets) avatarMessage;
        byte[]? fileData = Extensions.GetAvatarData(receiving.AvatarHash);
        if(fileData == null) return;
        GetAvatarDatas(fileData, datas => Extensions.SendInterval(fromPlayer, datas));
    }
}