using System;
using System.IO;
using GameNetcodeStuff;
using LethalAvatars.Libs;
using ProtoBuf;
using Avatar = LethalAvatars.SDK.Avatar;

namespace LethalAvatars.Networking.Messages;

[ProtoContract]
internal class SwitchAvatar : AvatarMessage
{
    public override string MessageName { get; set; } = $"{Plugin.GUID}.SwitchAvatar";
    [ProtoMember(7)] public string AvatarFileHash { get; set; } = String.Empty;

    protected override void Handle(PlayerControllerB player, AvatarMessage data)
    {
        SwitchAvatar switchAvatarMessage = (SwitchAvatar) data;
        if(player.GetIdentifier() == PlayerAvatarAPI.LocalPlayer!.GetIdentifier())
        {
            Plugin.PluginLogger.LogDebug("Cannot apply avatar to local player in remote scope");
            return;
        }
        // Don't switch avatars if its the same one
        if (PlayerAvatarAPI.cachedAvatarHashes.ContainsKey(player.GetIdentifier()) &&
            PlayerAvatarAPI.cachedAvatarHashes[player.GetIdentifier()] == switchAvatarMessage.AvatarFileHash)
            return;
        Plugin.PluginLogger.LogDebug($"Switching to avatar {AvatarFileHash} from {player.GetIdentifier()}");
        if (AvatarData.cachedDatas.ContainsKey(player.GetIdentifier()))
            AvatarData.cachedDatas.Remove(player.GetIdentifier());
        if (AvatarData.LastUpdates.ContainsKey(player.GetIdentifier()))
            AvatarData.LastUpdates.Remove(player.GetIdentifier());
        // Remove current avatar
        if (PlayerAvatarAPI.RegisteredAvatars.ContainsKey(player))
            PlayerAvatarAPI.ResetPlayer(player);
        if (PlayerAvatarAPI.TryGetCachedAvatar(switchAvatarMessage.AvatarFileHash, out Avatar? cachedAvatar))
        {
            PlayerAvatarAPI.ApplyNewAvatar(cachedAvatar!, player, switchAvatarMessage.AvatarFileHash);
            return;
        }
        string file = String.Empty;
        string h = String.Empty;
        foreach (string assetBundleFile in Directory.GetFiles(Plugin.AvatarsPath))
        {
            string hash = Extensions.GetHashOfFile(assetBundleFile);
            if(hash != switchAvatarMessage.AvatarFileHash) continue;
            file = assetBundleFile;
            h = hash;
            break;
        }
        if (string.IsNullOrEmpty(file))
        {
            // We don't have the avatar, request it
            Plugin.PluginLogger.LogWarning(
                $"Failed to find Avatar file for hash {switchAvatarMessage.AvatarFileHash}. Requesting from owner");
            AvatarData.cachedDatas.Add(player.GetIdentifier(), new());
            RequestAvatar requestAvatarData = new RequestAvatar
            {
                Hash = switchAvatarMessage.AvatarFileHash
            };
            requestAvatarData.Send(player);
            return;
        }
        // We have the avatar, load it
        Avatar? avatar = PlayerAvatarAPI.LoadAvatar(file);
        if (avatar == null)
        {
            Plugin.PluginLogger.LogError($"Failed to load avatar for {player.GetIdentifier()}");
            PlayerAvatarAPI.ResetPlayer(player);
            return;
        }
        PlayerAvatarAPI.ApplyNewAvatar(avatar, player, h);
    }
}