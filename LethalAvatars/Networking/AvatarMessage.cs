using System;
using System.Collections;
using GameNetcodeStuff;
using LethalAvatars.Libs;
using LethalAvatars.Networking.Messages;
using ProtoBuf;
using Unity.Netcode;
using UnityEngine;

namespace LethalAvatars.Networking;

[ProtoContract]
[ProtoInclude(21, typeof(AvatarData))]
[ProtoInclude(22, typeof(RequestAvatar))]
[ProtoInclude(23, typeof(SwitchAvatar))]
[ProtoInclude(25, typeof(AvatarParameters))]
[ProtoInclude(26, typeof(MissingAvatarDataPackets))]
internal abstract class AvatarMessage
{
    [ProtoMember(1)] public abstract string MessageName { get; set; }
    [ProtoMember(2)] private bool Forward { get; set; }
    [ProtoMember(3)] private string? SendToUsername { get; set; }
    [ProtoMember(4)] private string FromUsername { get; set; } = String.Empty;
    [ProtoMember(5)] private bool IncludeSelf { get; set; }
    [ProtoMember(6)] private NetworkDelivery Delivery { get; set; }

    protected abstract void Handle(PlayerControllerB fromPlayer, AvatarMessage avatarMessage);

    public void Broadcast(bool includeSelf = false, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
    {
        PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
        if (localPlayer == null)
        {
            LethalAvatarsRunner.Instance!.RunCoroutine(_c(() => Broadcast(includeSelf, networkDelivery)));
            return;
        }
        SendToUsername = null;
        Delivery = networkDelivery;
        FromUsername = localPlayer.GetIdentifier();
        IncludeSelf = includeSelf;
        // We're the server, let's broadcast and handle
        if (NetworkHandler.IsServer)
        {
            NetworkHandler.Broadcast(this, networkDelivery);
            return;
        }
        // Make it a forward broadcast
        Forward = true;
        NetworkHandler.Send(this, networkDelivery);
    }

    private IEnumerator _c(Action c)
    {
        PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
        yield return new WaitUntil(() =>
        {
            localPlayer = PlayerAvatarAPI.LocalPlayer;
            return localPlayer != null;
        });
        c.Invoke();
    }

    public void Send(PlayerControllerB target, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
    {
        PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
        if (localPlayer == null)
        {
            LethalAvatarsRunner.Instance!.RunCoroutine(_c(() => Send(target, networkDelivery)));
            return;
        }
        Delivery = networkDelivery;
        FromUsername = localPlayer.GetIdentifier();
        SendToUsername = target.GetIdentifier();
        // We're the server
        if (NetworkHandler.IsServer)
        {
            Forward = false;
            NetworkHandler.Broadcast(this, networkDelivery);
            return;
        }
        // We're not the server, we need to forward
        Forward = true;
        NetworkHandler.Send(this, networkDelivery);
    }

    internal void OnMessage(ulong senderId)
    {
        if (Forward && NetworkHandler.IsServer)
        {
            Forward = false;
            PlayerControllerB? playerControllerA = NetworkHandler.GetPlayerControllerFromId(FromUsername);
            if (playerControllerA == null)
                return;
            NetworkHandler.Broadcast(this, Delivery);
        }
        else if (Forward && !NetworkHandler.IsServer)
            Plugin.PluginLogger.LogWarning("Received forward message when not the server!");
        else
        {
            PlayerControllerB? playerController = NetworkHandler.GetPlayerControllerFromId(FromUsername);
            if (playerController == null)
                return;
            PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
            if (localPlayer == null)
            {
                Plugin.PluginLogger.LogWarning("Cannot handle message when LocalPlayer is null!");
                return;
            }
            // We were excluded from the broadcast
            if (SendToUsername == null && FromUsername == localPlayer.GetIdentifier() && !IncludeSelf) return;
            // This message isn't for us
            if(SendToUsername != null && SendToUsername != localPlayer.GetIdentifier()) return;
            // Don't send to self if we don't want it
            if(SendToUsername != null && FromUsername == localPlayer.GetIdentifier() && !IncludeSelf) return;
            Handle(playerController, this);
        }
    }
}