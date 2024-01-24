using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameNetcodeStuff;
using LethalAvatars.Libs;
using LethalAvatars.Networking.Messages;
using ProtoBuf;
using Unity.Collections;
using Unity.Netcode;

namespace LethalAvatars.Networking;

internal static class NetworkHandler
{
    public static bool IsServer
    {
        get
        {
            PlayerControllerB? player = PlayerAvatarAPI.LocalPlayer;
            if (player == null) return false;
            return player.IsMaster();
        }
    }

    private static PlayerControllerB? serverPlayer;
    public static PlayerControllerB? ServerPlayer
    {
        get
        {
            if (serverPlayer != null) return serverPlayer;
            try
            {
                PlayerControllerB playerControllerB =
                    PlayerAvatarAPI.GetAllPlayers().First(x => x.IsMaster());
                serverPlayer = playerControllerB;
                return serverPlayer;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    internal static List<PlayerControllerB> connectedPlayers = new();
    
    public static readonly List<AvatarMessage> MessageHandlers = new()
    {
        new AvatarData(),
        new RequestAvatar(),
        new SwitchAvatar(),
        new AvatarParameters(),
        new MissingAvatarDataPackets()
    };
    
    internal static void Initialize()
    {
        serverPlayer = null;
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MessageHandlers[0].MessageName,
            (id, payload) =>
            {
                AvatarData avatarData = ReadFromFastReader<AvatarData>(payload);
                avatarData.OnMessage(id);
            });
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MessageHandlers[1].MessageName,
            (id, payload) =>
            {
                RequestAvatar requestAvatar = ReadFromFastReader<RequestAvatar>(payload);
                requestAvatar.OnMessage(id);
            });
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MessageHandlers[2].MessageName,
            (id, payload) =>
            {
                SwitchAvatar switchAvatar = ReadFromFastReader<SwitchAvatar>(payload);
                switchAvatar.OnMessage(id);
            });
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MessageHandlers[3].MessageName,
            (id, payload) =>
            {
                AvatarParameters avatarParameters = ReadFromFastReader<AvatarParameters>(payload);
                avatarParameters.OnMessage(id);
            });
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MessageHandlers[4].MessageName,
            (id, payload) =>
            {
                MissingAvatarDataPackets missingAvatarDataPackets =
                    ReadFromFastReader<MissingAvatarDataPackets>(payload);
                missingAvatarDataPackets.OnMessage(id);
            });
    }
    
    internal static PlayerControllerB? GetPlayerControllerFromId(string id)
    {
        PlayerControllerB[] players = connectedPlayers.ToArray();
        try
        {
            return players.First(x => x.GetIdentifier() == id);
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static void Broadcast(AvatarMessage avatarMessage,
        NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
    {
        if(NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null) return;
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(avatarMessage.MessageName,
            WriteData(avatarMessage), networkDelivery);
    }
    
    internal static void Send(AvatarMessage avatarMessage, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
    {
        if(NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null) return;
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(avatarMessage.MessageName,
            NetworkManager.ServerClientId, WriteData(avatarMessage), networkDelivery);
    }

    private static FastBufferWriter WriteData<T>(T obj)
    {
        MemoryStream ms = new MemoryStream();
        Serializer.Serialize(ms, obj);
        byte[] data = ms.ToArray();
        int s = FastBufferWriter.GetWriteSize(data);
        FastBufferWriter writer = new FastBufferWriter(s, Allocator.Temp);
        writer.WriteValueSafe(data);
        ms.Dispose();
        return writer;
    }

    private static T ReadFromFastReader<T>(FastBufferReader fastBufferReader)
    {
        fastBufferReader.ReadValueSafe(out byte[] data);
        return Serializer.Deserialize<T>(new ReadOnlyMemory<byte>(data));
    }
}