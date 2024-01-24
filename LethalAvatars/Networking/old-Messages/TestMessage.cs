#if false
using ProtoBuf;

namespace LethalAvatars.Networking.Messages;

[ProtoContract]
public class TestMessage
{
    internal const string MESSAGE_NAME = Plugin.GUID + ".TestMessage";
    
    [ProtoMember(1)] public byte[] LargeData { get; set; }

    internal static byte[]? original;
    
    internal static void Handle(ulong id, TestMessage testMessage) =>
        Plugin.PluginLogger.LogInfo($"Got data with size {testMessage.LargeData.Length} from {id}");
}
#endif