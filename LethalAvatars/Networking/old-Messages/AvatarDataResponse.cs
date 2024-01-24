using System;
using GameNetcodeStuff;
using LethalAvatars.SDK;
using ProtoBuf;
using Unity.Netcode;

namespace LethalAvatars.Networking.Messages;

[ProtoContract]
internal class AvatarDataResponse
{
    internal const string MESSAGE_NAME = Plugin.GUID + ".AvatarDataResponse";
    
    [ProtoMember(1)] public byte[] Data { get; set; } = Array.Empty<byte>();
    [ProtoMember(2)] public bool Forward { get; set; }
    [ProtoMember(3)] public ulong ForwardingId { get; set; }
    [ProtoMember(4)] public ulong FromId { get; set; }

    internal static void Handle(ulong l, AvatarDataResponse avatarDataResponse)
    {
        if (avatarDataResponse.Forward)
        {
            AvatarDataResponse newAvatarDataResponse = new AvatarDataResponse
            {
                Data = avatarDataResponse.Data,
                FromId = l
            };
            NetworkHandler.Send(MESSAGE_NAME, avatarDataResponse.ForwardingId, newAvatarDataResponse,
                NetworkDelivery.ReliableFragmentedSequenced);
            return;
        }
        PlayerControllerB? player = PlayerAvatarAPI.GetPlayerControllerFromId(avatarDataResponse.FromId);
        if (player == null)
        {
            Plugin.PluginLogger.LogError($"Failed to find PlayerControllerB from Id {l}");
            return;
        }
        Avatar? avatar = PlayerAvatarAPI.LoadAvatar(avatarDataResponse.Data);
        avatarDataResponse.Data = Array.Empty<byte>();
        if (avatar == null)
        {
            Plugin.PluginLogger.LogError($"Failed to load avatar for {player.playerSteamId}");
            PlayerAvatarAPI.ResetPlayer(player);
            return;
        }
        PlayerAvatarAPI.ApplyNewAvatar(avatar, player);
    }
}