using System;
using System.IO;
using GameNetcodeStuff;
using ProtoBuf;
using Unity.Netcode;

namespace LethalAvatars.Networking.Messages;

[ProtoContract]
internal class RequestAvatarData
{
    internal const string MESSAGE_NAME = Plugin.GUID + ".RequestAvatarData";
    
    [ProtoMember(1)] public string Hash { get; set; } = String.Empty;
    [ProtoMember(2)] public bool Forward { get; set; }
    [ProtoMember(3)] public ulong ForwardingId { get; set; }
    [ProtoMember(4)] public ulong FromId { get; set; }
    
    internal static void Handle(ulong l, RequestAvatarData requestAvatarData)
    {
        if (requestAvatarData.Forward)
        {
            if(!NetworkHandler.IsServer) return;
            RequestAvatarData newRequestAvatarData = new RequestAvatarData
            {
                Hash = requestAvatarData.Hash,
                FromId = l
            };
            NetworkHandler.Send(MESSAGE_NAME, requestAvatarData.ForwardingId, newRequestAvatarData);
            return;
        }
        PlayerControllerB? player = PlayerAvatarAPI.GetPlayerControllerFromId(l);
        if (player == null)
        {
            Plugin.PluginLogger.LogError($"Failed to find PlayerControllerB from Id {l}");
            return;
        }
        string file = String.Empty;
        foreach (string assetBundleFile in Directory.GetFiles(Plugin.AvatarsPath))
        {
            string hash = PlayerAvatarAPI.GetHashOfFile(assetBundleFile);
            if(hash != requestAvatarData.Hash) continue;
            file = assetBundleFile;
            break;
        }
        if (string.IsNullOrEmpty(file))
        {
            Plugin.PluginLogger.LogError($"Failed to find Avatar file for hash {requestAvatarData.Hash}!");
            return;
        }
        FileStream fileStream =
            new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        byte[] data = new byte[fileStream.Length];
        int c = fileStream.Read(data, 0, data.Length);
        if (c != data.Length)
            throw new Exception($"Source byte length {fileStream.Length} did not match target {data.Length}");
        // Create the Avatar Data for Size Reference
        AvatarDataResponse avatarDataResponse = new AvatarDataResponse
        {
            Data = data,
            Forward = NetworkHandler.IsServer,
            FromId = PlayerAvatarAPI.LocalPlayer!.playerClientId,
            ForwardingId = l
        };
        // Update the NetworkSize
        UpdateNetworkSize updateNetworkSize = new UpdateNetworkSize
        {
            NewMaxMessageSize = data.Length + 1000,
            RequestBroadcast = true
        };
        NetworkHandler.Broadcast(UpdateNetworkSize.MESSAGE_NAME, updateNetworkSize);
        NetworkManager.Singleton.MaximumFragmentedMessageSize = updateNetworkSize.NewMaxMessageSize;
        // Send the Avatar Data
        NetworkHandler.Send(AvatarDataResponse.MESSAGE_NAME, l, avatarDataResponse,
            NetworkDelivery.ReliableFragmentedSequenced);
    }
}