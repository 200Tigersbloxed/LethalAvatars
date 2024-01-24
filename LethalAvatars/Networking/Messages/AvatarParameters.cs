using System.Collections.Generic;
using GameNetcodeStuff;
using LethalAvatars.Libs;
using ProtoBuf;
using UnityEngine;
using Avatar = LethalAvatars.SDK.Avatar;

namespace LethalAvatars.Networking.Messages;

[ProtoContract]
internal class AvatarParameters : AvatarMessage
{
    [ProtoMember(7)] public override string MessageName { get; set; } = $"{Plugin.GUID}.AvatarParameters";
    [ProtoMember(8)] public Dictionary<string, int> IntParameters = new();
    [ProtoMember(9)] public Dictionary<string, float> FloatParameters = new();
    [ProtoMember(10)] public Dictionary<string, bool> BoolParameters = new();
    [ProtoMember(11)] public float eulerX;
    [ProtoMember(12)] public float eulerY;
    [ProtoMember(13)] public float eulerZ;
    [ProtoMember(14)] public bool skipRot;
    [ProtoMember(15)] public bool twoHanded;
    
    public AvatarParameters(){}

    public AvatarParameters(Dictionary<string, object> parameters, Transform? camera, bool twoHanded)
    {
        if (camera != null)
        {
            Vector3 euler = camera.eulerAngles;
            eulerX = euler.x;
            eulerY = euler.y;
            eulerZ = euler.z;
            this.twoHanded = twoHanded;
        }
        else
            skipRot = true;
        foreach (KeyValuePair<string,object> keyValuePair in parameters)
        {
            switch (keyValuePair.Value)
            {
                case int i:
                    IntParameters.Add(keyValuePair.Key, i);
                    break;
                case float f:
                    FloatParameters.Add(keyValuePair.Key, f);
                    break;
                case bool b:
                    BoolParameters.Add(keyValuePair.Key, b);
                    break;
            }
        }
    }
    
    protected override void Handle(PlayerControllerB fromId, AvatarMessage avatarMessage)
    {
        PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
        if(localPlayer == null || fromId.GetIdentifier() == localPlayer.GetIdentifier()) return;
        if(!PlayerAvatarAPI.RegisteredAvatars.TryGetValue(fromId, out Avatar avatar)) return;
        AvatarDriver avatarDriver = avatar.GetComponent<AvatarDriver>();
        avatarDriver.twoHanded = twoHanded;
        Transform? head = avatarDriver.GetBoneFromHumanoid(HumanBodyBones.Head);
        if (head != null && !skipRot)
        {
            Quaternion rot = Quaternion.Euler(eulerX, eulerY, eulerZ);
            avatarDriver.headrot = new Quaternion(rot.x, rot.y, rot.z, rot.w);
        }
        foreach (KeyValuePair<string, int> keyValuePair in IntParameters)
            avatarDriver.SetParameter(keyValuePair.Key, keyValuePair.Value);
        foreach (KeyValuePair<string, float> keyValuePair in FloatParameters)
            avatarDriver.SetParameter(keyValuePair.Key, keyValuePair.Value);
        foreach (KeyValuePair<string, bool> keyValuePair in BoolParameters)
            avatarDriver.SetParameter(keyValuePair.Key, keyValuePair.Value);
    }
}