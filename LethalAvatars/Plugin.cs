using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalAvatars.GameUI;
using LethalAvatars.Libs;
using LethalAvatars.Networking;
using LethalAvatars.Networking.Messages;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LethalAvatars;

[BepInPlugin(GUID, NAME, VERSION)]
internal class Plugin : BaseUnityPlugin
{
    internal const string GUID = "gay.tigers.lethalavatars";
    internal const string NAME = "LethalAvatars";
    internal const string VERSION = "0.1.0";

    /// <summary>
    /// The location where Avatars are stored. Check if empty before using! Is initialized on BaseUnityPlugin.Awake()
    /// </summary>
    public static string AvatarsPath = String.Empty;

    /// <summary>
    /// Gets the file location of the selected Avatar
    /// </summary>
    public static string SelectedAvatar
    {
        get => PluginInstance!.config.SelectedAvatar;
        internal set
        {
            PluginInstance!.config.SelectedAvatar = value;
            PluginInstance.config.Save(ConfigLocation);
        }
    }
    
    // Why is this protected? Not every log will come from this class...
    internal static ManualLogSource PluginLogger => PluginInstance!.Logger;
    
    private static Plugin? PluginInstance;
    private static string ConfigLocation = String.Empty;

    private readonly Config config;
    private readonly Harmony Harmony = new(GUID);

    public Plugin()
    {
        DependencyManager.Initialize();
        PluginInstance = this;
        ConfigLocation = Path.Combine(Paths.ConfigPath, $"{GUID}.cfg");
        config = LethalAvatars.Config.Create(ConfigLocation);
        Harmony.PatchAll();
    }

    private void Awake()
    {
        // ew ../ sucks, oh well
        AvatarsPath = Application.dataPath + "/../Avatars";
        if (!Directory.Exists(AvatarsPath))
            Directory.CreateDirectory(AvatarsPath);
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == "SampleSceneRelay")
                NetworkHandler.Initialize();
            if (scene.name != "MainMenu")
            {
                if(UILoader.MainCanvas == null) return;
                UILoader.MainCanvas.gameObject.SetActive(false);
                return;
            }
            AvatarData.cachedDatas.Clear();
            AvatarData.LastUpdates.Clear();
            AvatarData.cachedAvatarData.Clear();
            PlayerAvatarAPI.cachedAvatarHashes.Clear();
            Extensions.chunkedAvatarData.Clear();
            if(UILoader.MainCanvas != null) return;
            // Setup Runner
            GameObject runner = new GameObject("LethalAvatarsRunner");
            runner.AddComponent<LethalAvatarsRunner>();
            UILoader.Prepare(scene);
        };
    }
}