using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LethalAvatars.SDK.Editor.Internals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LethalAvatars.SDK.Editor;

class BuilderWindow : EditorWindow
{
    private const string GITHUB_URL = "https://github.com/200Tigersbloxed/LethalAvatars";
    
    private static BuilderWindow Window;

    [MenuItem("LethalAvatars/Build")]
    private static void ShowWindow()
    {
        Window = GetWindow<BuilderWindow>();
        Window.titleContent = new GUIContent("Avatar Builder");
        if(preferences == null)
            preferences = Preferences.Create();
    }

    internal static void ShowWindow(Avatar avatar)
    {
        if (Window != null)
        {
            Window.SelectedAvatar = avatar;
            Window.Focus();
            return;
        }
        ShowWindow();
        Window.SelectedAvatar = avatar;
    }

    private static Preferences preferences;

    static BuilderWindow()
    {
        PackageInstaller.OnLoaded += () => waitingForPackage = false;
        AssetDatabase.importPackageCompleted += _ => hdrpaType = Reflecting.FindType("HDRenderPipelineAsset");
        hdrpaType = Reflecting.FindType("HDRenderPipelineAsset");
    }

    private static Vector2 avatarListScroll;
    private static bool isBuilding;
    private static bool waitingForPackage;
    private static Type hdrpaType;
    private static Object hdrpAsset;

    private Avatar SelectedAvatar;
    internal static List<Avatar> Avatars = new();
    private static string outputDirectory;

    private static bool hasLooked;

    private string BuildAvatar(DirectoryIdentifier directoryIdentifier, Avatar avatar)
    {
        isBuilding = true;
        string avatarName = NameTools.GetSafeName(avatar.gameObject.name);
        string prefabLocation = directoryIdentifier.GetFilePath($"{avatarName}.prefab");
        PrefabUtility.SaveAsPrefabAsset(avatar.gameObject, prefabLocation, out bool savedPrefab);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (savedPrefab && File.Exists(prefabLocation))
        {
            string[] assets = {prefabLocation};
            AssetBundleBuild[] builds =
            {
                new()
                {
                    assetBundleName = avatarName,
                    assetNames = assets,
                    assetBundleVariant = "lca"
                }
            };
            string assetBundleDir = directoryIdentifier.CreateSubDirectory("assetbundle");
            BuildPipeline.BuildAssetBundles(assetBundleDir, builds, BuildAssetBundleOptions.ChunkBasedCompression,
                BuildTarget.StandaloneWindows64);
            isBuilding = false;
            return Path.Combine(assetBundleDir, $"{avatarName}.lca");
        }
        isBuilding = false;
        Debug.LogError("Failed to save Avatar to prefab");
        return String.Empty;
    }
    
    private bool VerifyProject()
    {
        if (waitingForPackage)
        {
            GUILayout.Label("Waiting for packages to install...", EditorStyles.centeredGreyMiniLabel);
            return false;
        }
        if (hdrpaType == null)
        {
            EditorGUILayout.HelpBox("HDRP is not installed! You must use HDRP in order to build any asset!",
                MessageType.Error);
            EditorGUILayout.HelpBox("If you're using Unity Hub, to create a 3D HDRP template from Projects, " +
                                    "click New Project, Select the correct Editor Version, and from the All templates " +
                                    "tab on the left, select 3D (HDRP), then re-import all your assets.",
                MessageType.Info);
            if (GUILayout.Button("Fix (Not Recommended)"))
            {
                bool ok = EditorUtility.DisplayDialog("LethalAvatars.SDK.Editor",
                    "Automatically fixing HDRP is not recommended, as settings may be left misconfigured. " +
                    "You should instead create a new HDRP template and re-import assets.",
                    "I don't care, do it anyways", "Cancel");
                if (ok)
                {
                    PackageInstaller.AddPackage("com.unity.render-pipelines.high-definition");
                    waitingForPackage = true;
                }
            }
            return false;
        }
        if (GraphicsSettings.currentRenderPipeline == null || 
            !GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("HDRenderPipelineAsset"))
        {
            EditorGUILayout.HelpBox("HDRP is not the current RenderPipeline!", MessageType.Error);
            if (GUILayout.Button("Create and Fix"))
            {
                RenderPipelineAsset renderPipelineAsset = (RenderPipelineAsset) CreateInstance(hdrpaType);
                if (renderPipelineAsset != null)
                {
                    if (File.Exists("Assets/HDRP.asset"))
                    {
                        bool ok3 = EditorUtility.DisplayDialog("LethalAvatars.SDK.Editor",
                            "A file is already located at Assets/HDRP.asset! Would you like to delete it?", "Yes",
                            "No");
                        if (!ok3)
                        {
                            Debug.LogWarning("Could not switch to HDRP since an Asset was not created");
                            return false;
                        }
                    }
                    AssetDatabase.CreateAsset(renderPipelineAsset, "Assets/HDRP.asset");
                    GraphicsSettings.defaultRenderPipeline = renderPipelineAsset;
                    QualitySettings.renderPipeline = renderPipelineAsset;
                    EditorUtility.DisplayDialog("LethalAvatars.SDK.Editor",
                        "Created HDRP asset at Assets/HDRP.asset!", "OK");
                    Reflecting.InvokePrivateMethod(Reflecting.FindType("HDWizard"), "OpenWindow",
                        Array.Empty<object>());
                }
            }
            EditorGUILayout.Separator();
            GUILayout.Label("OR", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Separator();
            GUILayout.Label("Select an existing HDRP Asset");
            hdrpAsset = EditorGUILayout.ObjectField("HDRP Asset", hdrpAsset, hdrpaType, false);
            if (hdrpAsset != null)
            {
                GraphicsSettings.defaultRenderPipeline = (RenderPipelineAsset) hdrpAsset;
                QualitySettings.renderPipeline = (RenderPipelineAsset) hdrpAsset;
                hdrpAsset = null;
            }
            return false;
        }
        return true;
    }

    private bool VerifyAvatar()
    {
        bool problem = false;
        if (SelectedAvatar.Viewpoint == null)
        {
            problem = true;
            EditorGUILayout.HelpBox("No Viewpoint attachment! This is required to build.", MessageType.Error);
        }
        if (SelectedAvatar.SmallItemGrab == null)
        {
            problem = true;
            EditorGUILayout.HelpBox("No Small Item attachment! This is required to build.", MessageType.Error);
        }
        return !problem;
    }

    private void DrawCurrentAvatar()
    {
        if(VerifyAvatar())
        {
            GUILayout.Label("Selected Avatar", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label($"Avatar Name: {SelectedAvatar.AvatarName}");
            GUILayout.Label($"Avatar Creator: {SelectedAvatar.AvatarCreator}");
            GUILayout.Label($"Avatar Description: {SelectedAvatar.AvatarDescription}");
            EditorGUILayout.Separator();
            if (!isBuilding)
            {
                if (GUILayout.Button("Build!"))
                {
                    string dir = outputDirectory;
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    DirectoryIdentifier directoryIdentifier = new DirectoryIdentifier();
                    string file = BuildAvatar(directoryIdentifier, SelectedAvatar);
                    if (File.Exists(file))
                    {
                        bool good = true;
                        string newFileLocation = Path.Combine(dir, Path.GetFileName(file));
                        if (File.Exists(newFileLocation))
                        {
                            bool ok = EditorUtility.DisplayDialog("LethalAvatars.SDK.Editor",
                                $"There is already a file with the name {Path.GetFileName(newFileLocation)} at the directory {dir}. " +
                                "Would you like to delete the file?", "Yes", "No");
                            if (ok)
                                File.Delete(newFileLocation);
                            else
                            {
                                Debug.LogWarning(
                                    "Could not save AssetBundle because one already exists in the same directory");
                                good = false;
                            }
                        }

                        if (good)
                        {
                            File.Copy(file, newFileLocation);
                            directoryIdentifier.Dispose();
                            EditorUtility.DisplayDialog("LethalAvatars.SDK.Editor",
                                $"Avatar Built! Find the file at {newFileLocation}",
                                "OK");
                        }
                        else
                            EditorUtility.DisplayDialog("LethalAvatars.SDK.Editor",
                                "Failed to Build AssetBundle! Check Console for more information.", "OK");

                        return;
                    }

                    directoryIdentifier.Dispose();
                    EditorUtility.DisplayDialog("LethalAvatars.SDK.Editor",
                        "Failed to Build AssetBundle! Check Console for more information.", "OK");
                }

                if (GUILayout.Button("Return"))
                    SelectedAvatar = null;
            }
            else
                GUILayout.Label("Building Avatar...", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            if (GUILayout.Button("Select Avatar"))
                Selection.activeGameObject = SelectedAvatar.gameObject;
            if (GUILayout.Button("Return"))
                SelectedAvatar = null;
        }
    }

    private void DrawAvatarList()
    {
        avatarListScroll = EditorGUILayout.BeginScrollView(avatarListScroll);
        foreach (Avatar avatar in new List<Avatar>(Avatars))
            if (GUILayout.Button(avatar.gameObject.name))
                SelectedAvatar = avatar;
        EditorGUILayout.EndScrollView();
    }

    private void OnGUI()
    {
        GUILayout.Label("LethalAvatars.SDK", EditorStyles.largeLabel);
        GUILayout.Label("Created by 200Tigersbloxed");
        if(GUILayout.Button("GitHub"))
            Application.OpenURL(GITHUB_URL);
        if (!VerifyProject()) return;
        Avatars = Avatars.Where(x => x != null).ToList();
        if(SelectedAvatar != null)
            DrawCurrentAvatar();
        else
        {
            if (Avatars.Count <= 0)
                Avatars = FindObjectsOfType<Avatar>(true).ToList();
            GUILayout.Label("Avatars", EditorStyles.centeredGreyMiniLabel);
            DrawAvatarList();
            EditorGUILayout.Separator();
            GUILayout.Label("Global Build Settings", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Output File Location");
            outputDirectory = GUILayout.TextField(outputDirectory);
            if (string.IsNullOrEmpty(outputDirectory))
                outputDirectory = preferences.OutputLocation;
            if (!hasLooked && string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                string gameDir = SteamHelper.GetGameLocation();
                if (!string.IsNullOrEmpty(gameDir))
                {
                    string avatarsDir = Path.Combine(gameDir, "Avatars");
                    if (!Directory.Exists(avatarsDir))
                        Directory.CreateDirectory(avatarsDir);
                    outputDirectory = avatarsDir;
                }
                hasLooked = true;
            }
            if (outputDirectory != preferences.OutputLocation)
            {
                preferences.OutputLocation = outputDirectory;
                preferences.Save();
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Select Directory"))
                outputDirectory = EditorUtility.OpenFolderPanel("Select the Output Directory", "", "");
        }
    }
}