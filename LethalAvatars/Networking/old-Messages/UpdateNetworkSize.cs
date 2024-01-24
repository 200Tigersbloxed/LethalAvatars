using ProtoBuf;
using Unity.Netcode;

namespace LethalAvatars.Networking.Messages;

[ProtoContract]
internal class UpdateNetworkSize
{
    internal const string MESSAGE_NAME = Plugin.GUID + ".UpdateNetworkSize";

    [ProtoMember(1)] public int NewMaxMessageSize { get; set; } = NetworkManager.Singleton.MaximumFragmentedMessageSize;
    [ProtoMember(2)] public bool RequestBroadcast;

    internal static void Handle(ulong l, UpdateNetworkSize updateNetworkSize)
    {
        NetworkManager.Singleton.MaximumFragmentedMessageSize = updateNetworkSize.NewMaxMessageSize;
        if (!updateNetworkSize.RequestBroadcast || !NetworkManager.Singleton.IsServer) return;
        UpdateNetworkSize newUpdateNetworkSize = new UpdateNetworkSize
            {NewMaxMessageSize = updateNetworkSize.NewMaxMessageSize};
        NetworkHandler.Broadcast(MESSAGE_NAME, newUpdateNetworkSize);
    }
}