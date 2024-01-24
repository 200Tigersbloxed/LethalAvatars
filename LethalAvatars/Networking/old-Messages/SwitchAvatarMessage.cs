using System;
using System.IO;
using GameNetcodeStuff;
using LethalAvatars.SDK;
using ProtoBuf;

namespace LethalAvatars.Networking.Messages;

[ProtoContract]
internal class SwitchAvatarMessage
{
    internal const string MESSAGE_NAME = Plugin.GUID + ".SwitchAvatar";
    
    [ProtoMember(1)] public string AvatarFileHash { get; set; } = String.Empty;
    [ProtoMember(2)] public bool Forward { get; set; }
    [ProtoMember(3)] public ulong FromId { get; set; }
    [ProtoMember(4)] public ulong? ForwardingId { get; set; } = null;
    
    internal static void Handle(ulong l, SwitchAvatarMessage switchAvatarMessage)
    {
        // Check if this is a forward message
        if (switchAvatarMessage.Forward)
        {
            if(!NetworkHandler.IsServer) return;
            SwitchAvatarMessage newRequestAvatarData = new SwitchAvatarMessage
            {
                AvatarFileHash = switchAvatarMessage.AvatarFileHash,
                FromId = l
            };
            if(switchAvatarMessage.ForwardingId == null)
            {
                NetworkHandler.Broadcast(MESSAGE_NAME, newRequestAvatarData);
                return;
            }
            NetworkHandler.Send(MESSAGE_NAME, switchAvatarMessage.ForwardingId.Value, newRequestAvatarData);
            return;
        }
        // Not forwarding, check if we have the avatar already
        PlayerControllerB? player = PlayerAvatarAPI.GetPlayerControllerFromId(switchAvatarMessage.FromId);
        if (player == null)
        {
            Plugin.PluginLogger.LogError($"Failed to find PlayerControllerB from Id {switchAvatarMessage.FromId}");
            return;
        }
        string file = String.Empty;
        foreach (string assetBundleFile in Directory.GetFiles(Plugin.AvatarsPath))
        {
            string hash = PlayerAvatarAPI.GetHashOfFile(assetBundleFile);
            if(hash != switchAvatarMessage.AvatarFileHash) continue;
            file = assetBundleFile;
            break;
        }
        if (string.IsNullOrEmpty(file))
        {
            // We don't have the avatar, request it
            Plugin.PluginLogger.LogWarning(
                $"Failed to find Avatar file for hash {switchAvatarMessage.AvatarFileHash}. Requesting from owner");
            RequestAvatarData requestAvatarData = new RequestAvatarData
            {
                Hash = switchAvatarMessage.AvatarFileHash,
                Forward = !NetworkHandler.IsServer,
                ForwardingId = switchAvatarMessage.FromId,
                FromId = PlayerAvatarAPI.LocalPlayer!.playerClientId
            };
            NetworkHandler.Send(RequestAvatarData.MESSAGE_NAME, switchAvatarMessage.FromId, requestAvatarData);
            return;
        }
        // We have the avatar, load it
        Avatar? avatar = PlayerAvatarAPI.LoadAvatar(file);
        if (avatar == null)
        {
            Plugin.PluginLogger.LogError($"Failed to load avatar for {player.playerSteamId}");
            PlayerAvatarAPI.ResetPlayer(player);
            return;
        }
        PlayerAvatarAPI.ApplyNewAvatar(avatar, player);
    }
}