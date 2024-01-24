using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using LethalAvatars.GameUI;
using LethalAvatars.Libs;
using ProtoBuf;
using Unity.Netcode;

namespace LethalAvatars.Networking.Messages;

[ProtoContract]
internal class AvatarData : AvatarMessage
{
    private const int PACKET_TIME_FRAME = 5;
    
    internal static Dictionary<string, List<AvatarData>> cachedDatas = new();
    internal static Dictionary<string, DateTime> LastUpdates = new();
    internal static Dictionary<string, byte[]> cachedAvatarData = new();
    
    public override string MessageName { get; set; } = $"{Plugin.GUID}.AvatarData";
    [ProtoMember(7)] public byte[] Data { get; set; } = Array.Empty<byte>();
    [ProtoMember(8)] public int PacketNumber { get; set; }
    [ProtoMember(9)] public int MaxPacketNumbers { get; set; }
    [ProtoMember(10)] public string AvatarHash = String.Empty;

    protected override void Handle(PlayerControllerB player, AvatarMessage data)
    {
        PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
        if(localPlayer == null || player.GetIdentifier() == localPlayer.GetIdentifier())
        {
            Plugin.PluginLogger.LogDebug("Failed to handle AvatarData!");
            return;
        }
        AvatarData avatarDataResponse = (AvatarData) data;
        if (!cachedDatas.ContainsKey(player.GetIdentifier())) return;
        List<AvatarData> avatarMessages = new List<AvatarData>(cachedDatas[player.GetIdentifier()]);
        if(avatarMessages.Count(x => x.PacketNumber == avatarDataResponse.PacketNumber) > 0)
            return;
        avatarMessages.Add(avatarDataResponse);
        LoadingNameplate.Apply(player, (float) avatarMessages.Count / avatarDataResponse.MaxPacketNumbers);
        if(!LastUpdates.ContainsKey(player.GetIdentifier()))
            LastUpdates.Add(player.GetIdentifier(), DateTime.Now);
        else
            LastUpdates[player.GetIdentifier()] = DateTime.Now;
        if (avatarMessages.Count >= avatarDataResponse.MaxPacketNumbers)
        {
            List<byte> cd = new();
            foreach (AvatarData avatarData in avatarMessages.OrderBy(x => x.PacketNumber).ToArray())
            {
                for (int i = 0; i < avatarData.Data.Length; i++)
                {
                    byte b = avatarData.Data[i];
                    cd.Add(b);
                }
            }
            byte[] combinedData = cd.ToArray();
            Extensions.LoadFromMemory(player, combinedData);
            cachedDatas.Remove(player.GetIdentifier());
            if (LastUpdates.ContainsKey(player.GetIdentifier()))
                LastUpdates.Remove(player.GetIdentifier());
            if (cachedAvatarData.ContainsKey(player.GetIdentifier()))
                cachedAvatarData.Remove(player.GetIdentifier());
            cachedAvatarData.Add(player.GetIdentifier(), combinedData);
            LoadingNameplate.Finish(player);
            return;
        }
        cachedDatas[player.GetIdentifier()] = avatarMessages;
    }

    internal static void Update()
    {
        DateTime now = DateTime.Now;
        foreach (KeyValuePair<string, DateTime> lastUpdate in new Dictionary<string, DateTime>(LastUpdates))
        {
            TimeSpan offset = now - lastUpdate.Value;
            // Packet has not been received for more than PACKET_TIME_FRAME seconds; clear and request old packets
            if (offset.Seconds > PACKET_TIME_FRAME)
            {
                if (!cachedDatas.ContainsKey(lastUpdate.Key))
                {
                    Plugin.PluginLogger.LogDebug("Cannot request packets when not waiting");
                    // The avatar loaded; abort
                    if (LastUpdates.ContainsKey(lastUpdate.Key))
                        LastUpdates.Remove(lastUpdate.Key);
                    return;
                }
                List<AvatarData> avatarMessages = new List<AvatarData>(cachedDatas[lastUpdate.Key]);
                // Shouldn't happen, but if it does, just don't do anything
                if(avatarMessages.Count <= 0)
                {
                    Plugin.PluginLogger.LogDebug("Cannot request packets when no packets loaded");
                    return;
                }
                List<int> gatheredPackets = new();
                foreach (AvatarData avatarMessage in avatarMessages)
                    gatheredPackets.Add(avatarMessage.PacketNumber);
                List<int> missingPackets = new();
                for (int i = 0; i < avatarMessages[0].MaxPacketNumbers; i++)
                {
                    if(gatheredPackets.Contains(i)) continue;
                    missingPackets.Add(i);
                }
                MissingAvatarDataPackets missingAvatarDataPackets = new MissingAvatarDataPackets
                {
                    MissingDatas = missingPackets,
                    AvatarHash = avatarMessages[0].AvatarHash
                };
                Plugin.PluginLogger.LogDebug(
                    $"Requesting {missingPackets.Count} missing packets from {lastUpdate.Key}");
                LastUpdates.Remove(lastUpdate.Key);
                missingAvatarDataPackets.Send(NetworkHandler.GetPlayerControllerFromId(lastUpdate.Key)!,
                    NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
    }
}