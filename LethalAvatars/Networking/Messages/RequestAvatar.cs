using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using LethalAvatars.Libs;
using ProtoBuf;

namespace LethalAvatars.Networking.Messages;

[ProtoContract]
internal class RequestAvatar : AvatarMessage
{
    public override string MessageName { get; set; } = $"{Plugin.GUID}.RequestAvatar";
    [ProtoMember(7)] public string Hash { get; set; } = String.Empty;
    
    protected override void Handle(PlayerControllerB player, AvatarMessage data)
    {
        RequestAvatar requestAvatarData = (RequestAvatar) data;
        byte[]? fileData = Extensions.GetAvatarData(requestAvatarData.Hash);
        if(fileData == null) return;
        // Create the Avatar Data for Size Reference
        Extensions.ChunkData(fileData, datas =>
        {
            List<AvatarData> avatarDatas = new();
            for (int i = 0; i < datas.Count; i++)
            {
                byte[] currentData = datas.ElementAt(i);
                AvatarData avatarDataResponse = new AvatarData
                {
                    Data = currentData,
                    PacketNumber = i,
                    MaxPacketNumbers = datas.Count,
                    AvatarHash = requestAvatarData.Hash
                };
                avatarDatas.Add(avatarDataResponse);
            }
            Extensions.SendInterval(player, avatarDatas.ToArray());
        });
    }
}