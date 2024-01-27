using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameNetcodeStuff;
using LethalAvatars.Libs;
using LethalAvatars.Networking.Messages;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Avatar = LethalAvatars.SDK.Avatar;
using Object = UnityEngine.Object;

namespace LethalAvatars;

public static class PlayerAvatarAPI
{
    private static Dictionary<PlayerControllerB, Avatar> registeredAvatars = new();
    public static Dictionary<PlayerControllerB, Avatar> RegisteredAvatars => new(registeredAvatars);

    internal static Dictionary<string, string> cachedAvatarHashes = new();

    private static Dictionary<string, Avatar> cachedAvatars = new();

    /// <summary>
    /// Gets the current LocalPlayer. Null if no LocalPlayer exists or if not in the GameScene.
    /// </summary>
    public static PlayerControllerB? LocalPlayer => GameNetworkManager.Instance.localPlayerController;

    private static Terminal? terminal;
    public static Terminal? Terminal
    {
        get
        {
            if (terminal == null)
                terminal = Object.FindObjectOfType<Terminal>();
            return terminal;
        }
    }

    public static PlayerControllerB[] GetAllPlayers() => Object.FindObjectsOfType<PlayerControllerB>();

    private static Avatar? LoadAvatar(AssetBundle assetBundle, string assetBundleHash)
    {
        Avatar avatar;
        try
        {
            avatar = assetBundle.LoadAllAssets<GameObject>().First().GetComponent<Avatar>();
        }
        catch (Exception)
        {
            // Probably failed to find the avatar
            return null;
        }
        cachedAvatars.Add(assetBundleHash, avatar);
        return Object.Instantiate(avatar.gameObject).GetComponent<Avatar>();
    }

    /// <summary>
    /// Loads an Avatar AssetBundle from file path
    /// </summary>
    /// <param name="fileLocation">The file to load</param>
    /// <returns>Nullable Avatar; null if failed to load</returns>
    public static Avatar? LoadAvatar(string fileLocation)
    {
        string hash = Extensions.GetHashOfFile(fileLocation);
        if (cachedAvatars.TryGetValue(hash, out Avatar loadedAvatar))
        {
            if(loadedAvatar == null)
            {
                cachedAvatars.Remove(hash);
                return null;
            }
            return Object.Instantiate(loadedAvatar.gameObject).GetComponent<Avatar>();
        }
        try
        {
            AssetBundle assetBundle = AssetBundle.LoadFromFile(fileLocation);
            return assetBundle == null ? null : LoadAvatar(assetBundle, hash);
        }
        catch (Exception)
        {
            Plugin.PluginLogger.LogError($"Failed to load AssetBundle at {fileLocation}");
            return null;
        }
    }

    /// <summary>
    /// Loads an Avatar AssetBundle from memory
    /// </summary>
    /// <param name="avatarData">AssetBundle data</param>
    /// <returns>Nullable Avatar; null if failed to load</returns>
    public static Avatar? LoadAvatar(byte[] avatarData)
    {
        string hash = Extensions.GetHashOfData(avatarData);
        if (cachedAvatars.TryGetValue(hash, out Avatar loadedAvatar))
            return Object.Instantiate(loadedAvatar.gameObject).GetComponent<Avatar>();
        try
        {
            AssetBundle assetBundle = AssetBundle.LoadFromMemory(avatarData);
            return assetBundle == null ? null : LoadAvatar(assetBundle, hash);
        }
        catch (Exception)
        {
            Plugin.PluginLogger.LogError("Failed to load AssetBundle from Memory");
            return null;
        }
    }

    private static List<AnimatorPlayer> InitializeAnimatorControllers(Avatar avatar)
    {
        List<AnimatorPlayer> animatorControllers = new List<AnimatorPlayer>();
        Animator animator = avatar.GetComponent<Animator>();
        if (animator == null) return animatorControllers;
        foreach (RuntimeAnimatorController animatorController in avatar.Animators)
        {
            PlayableGraph playableGraph;
            AnimatorControllerPlayable controllerPlayable =
                AnimationPlayableUtilities.PlayAnimatorController(animator, animatorController,
                    out playableGraph);
            animatorControllers.Add(new AnimatorPlayer(avatar, animatorController, controllerPlayable,
                playableGraph));
        }
        return animatorControllers;
    }

    /// <summary>
    /// Applies an Avatar to a player's model
    /// </summary>
    /// <param name="clonedAvatar">The Avatar to use. Do NOT use the original from the AssetBundle.</param>
    /// <param name="player">The player to apply the Avatar to</param>
    /// <param name="hash">The MD5 hash of the data for the Avatar</param>
    public static void ApplyNewAvatar(Avatar clonedAvatar, PlayerControllerB player, string hash)
    {
        // Disable and rename old stuff
        Transform scav = player.transform.Find("ScavengerModel");
        for (int i = 0; i < scav.childCount; i++)
        {
            Transform child = scav.GetChild(i);
            if(!child.gameObject.name.Contains("LOD")) continue;
            child.gameObject.GetComponent<SkinnedMeshRenderer>().enabled = false;
            child.gameObject.SetActive(false);
        }
        Transform metarig = scav.Find("metarig");
        Transform armsMetaRig = metarig.Find("ScavengerModelArmsOnly/Circle");
        armsMetaRig.GetComponent<SkinnedMeshRenderer>().enabled = false;
        Transform spine003 = metarig.Find("spine/spine.001/spine.002/spine.003");
        spine003.Find("LevelSticker").gameObject.SetActive(false);
        spine003.Find("BetaBadge").gameObject.SetActive(false);
        // Add new stuff
        clonedAvatar.gameObject.name = "avatar";
        clonedAvatar.transform.SetParent(metarig.parent);
        clonedAvatar.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        AvatarDriver avatarDriver = clonedAvatar.gameObject.AddComponent<AvatarDriver>();
        avatarDriver.player = player;
        avatarDriver.Avatar = clonedAvatar;
        avatarDriver.SetupAvatar(clonedAvatar.GetComponent<Animator>());
        avatarDriver.animators = InitializeAnimatorControllers(clonedAvatar);
        avatarDriver.lastLocalItemGrab = player.localItemHolder;
        avatarDriver.lastServerItemGrab = player.serverItemHolder;
        Transform cameraTransform = metarig.Find("CameraContainer/MainCamera");
        avatarDriver.AnimationDone(clonedAvatar.GetComponent<Animator>(), cameraTransform, player.IsLocal());
        registeredAvatars.Add(player, clonedAvatar);
        if (cachedAvatarHashes.ContainsKey(player.GetIdentifier()))
            cachedAvatarHashes[player.GetIdentifier()] = hash;
        else
            cachedAvatarHashes.Add(player.GetIdentifier(), hash);
        if(!player.IsLocal()) return;
        GameObject systemsObject = SceneManager.GetActiveScene().GetRootGameObjects().First(x => x.name == "Systems");
        Transform helmet = systemsObject.transform.Find("Rendering/PlayerHUDHelmetModel/ScavengerHelmet");
        RenderSpecificCamera renderSpecificCamera = helmet.gameObject.GetComponent<RenderSpecificCamera>();
        if (renderSpecificCamera == null)
            renderSpecificCamera = helmet.gameObject.AddComponent<RenderSpecificCamera>();
        Camera targetCamera = cameraTransform.GetComponent<Camera>();
        renderSpecificCamera.RenderCameras.Add(targetCamera);
        renderSpecificCamera.TargetRenderer = helmet.GetComponent<MeshRenderer>();
        renderSpecificCamera.OnHide += (camera, renderer) =>
        {
            if (camera != targetCamera) return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        };
        renderSpecificCamera.OnShow += (camera, renderer) =>
        {
            if (camera != targetCamera) return;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        };
    }

    /// <summary>
    /// Reverts all modified objects when applying an Avatar to a player
    /// </summary>
    /// <param name="player">The player to revert</param>
    public static void ResetPlayer(PlayerControllerB player)
    {
        Transform scav = player.transform.Find("ScavengerModel");
        if(scav == null)
        {
            Plugin.PluginLogger.LogWarning("Could not find ScavengerModel! Cannot ResetPlayer.");
            return;
        }
        for (int i = 0; i < scav.childCount; i++)
        {
            Transform child = scav.GetChild(i);
            if(!child.gameObject.name.Contains("LOD")) continue;
            child.gameObject.GetComponent<SkinnedMeshRenderer>().enabled = true;
            child.gameObject.SetActive(true);
        }
        for (int i = 0; i < scav.childCount; i++)
        {
            Transform avatar = scav.GetChild(i);
            if(avatar.GetComponent<Avatar>() == null) continue;
            Object.DestroyImmediate(avatar.gameObject);
            registeredAvatars.Remove(player);
            if(player != LocalPlayer) return;
            Transform metarig = scav.Find("metarig");
            Transform armsMetaRig = metarig.Find("ScavengerModelArmsOnly/Circle");
            armsMetaRig.GetComponent<SkinnedMeshRenderer>().enabled = true;
            Transform mainCamera = metarig.Find("CameraContainer/MainCamera");
            mainCamera.localPosition = Vector3.zero;
            GameObject systemsObject =
                SceneManager.GetActiveScene().GetRootGameObjects().First(x => x.name == "Systems");
            Transform helmet = systemsObject.transform.Find("Rendering/PlayerHUDHelmetModel/ScavengerHelmet");
            RenderSpecificCamera renderSpecificCamera = helmet.gameObject.GetComponent<RenderSpecificCamera>();
            if (renderSpecificCamera != null)
                Object.DestroyImmediate(renderSpecificCamera);
        }
    }
    
    public static void Reset()
    {
        foreach (PlayerControllerB player in GetAllPlayers())
        {
            if(!RegisteredAvatars.ContainsKey(player)) continue;
            try{ResetPlayer(player);}catch(Exception){}
        }
        registeredAvatars.Clear();
        terminal = null;
    }

    internal static void RefreshAllAvatars()
    {
        foreach (PlayerControllerB player in GetAllPlayers())
        {
            // No avatar in the first place
            if(!RegisteredAvatars.ContainsKey(player))
                return;
            // No refresh needed if we still have an avatar
            if (RegisteredAvatars.ContainsKey(player) && RegisteredAvatars[player] != null)
                return;
            if(cachedAvatarHashes.ContainsKey(player.GetIdentifier()))
            {
                string hash = cachedAvatarHashes[player.GetIdentifier()];
                string file = String.Empty;
                foreach (string asset in Directory.GetFiles(Plugin.AvatarsPath))
                {
                    if (!Path.GetExtension(asset).Contains("lca")) continue;
                    string h = Extensions.GetHashOfFile(asset);
                    if (h != hash) continue;
                    file = asset;
                    break;
                }
                if (!string.IsNullOrEmpty(file))
                {
                    Avatar? avatar = LoadAvatar(file);
                    if (avatar != null)
                        ApplyNewAvatar(avatar, player, hash);
                    return;
                }
            }
            byte[]? data = null;
            foreach (KeyValuePair<string,byte[]> keyValuePair in AvatarData.cachedAvatarData)
            {
                if(keyValuePair.Key != player.GetIdentifier()) continue;
                data = keyValuePair.Value;
            }
            if (data != null)
            {
                Avatar? avatar = LoadAvatar(data);
                if(avatar != null)
                    ApplyNewAvatar(avatar, player, Extensions.GetHashOfData(data));
                return;
            }
            Plugin.PluginLogger.LogDebug($"Failed to find cached avatar hash for {player.GetIdentifier()}");
        }
    }
}