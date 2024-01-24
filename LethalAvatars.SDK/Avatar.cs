using System.Collections.Generic;
using UnityEngine;

namespace LethalAvatars.SDK;

public class Avatar : MonoBehaviour
{
    public Transform Viewpoint;
    public Transform SmallItemGrab;
    public Transform BigItemGrab;
    public List<RuntimeAnimatorController> Animators = new();

    public string AvatarName;
    public string AvatarCreator;
    public Sprite AvatarIcon;
    public string AvatarDescription;
    public bool AllowDownloading;
}