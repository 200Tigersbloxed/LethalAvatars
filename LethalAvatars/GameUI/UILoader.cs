using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Avatar = LethalAvatars.SDK.Avatar;
using Object = UnityEngine.Object;

namespace LethalAvatars.GameUI;

internal class UILoader
{
    internal static Canvas? MainCanvas;
    internal static TMP_FontAsset? font;
    private static Transform? Bcg;
    private static Transform? AvatarsListContent;
    private static Transform? SelectedAvatar;
    
    private static TMP_Text? AvatarNameText;
    private static TMP_Text? AvatarCreatorText;
    private static Image? AvatarIcon;
    private static TMP_Text? AvatarDescriptionText;

    public static (string, Avatar?) CurrentlySelectedAvatar
    {
        get => currentlySelectedAvatar;
        set
        {
            if (value.Item2 == null)
            {
                SelectedAvatar!.gameObject.SetActive(false);
                currentlySelectedAvatar = value;
                return;
            }
            AvatarNameText!.text = value.Item2.AvatarName;
            AvatarCreatorText!.text = value.Item2.AvatarCreator;
            AvatarIcon!.sprite = value.Item2.AvatarIcon;
            AvatarDescriptionText!.text = value.Item2.AvatarDescription;
            SelectedAvatar!.gameObject.SetActive(true);
            currentlySelectedAvatar = value;
        }
    }
    private static (string, Avatar?) currentlySelectedAvatar = (String.Empty, null);

    public static void RefreshAvatars()
    {
        while (AvatarsListContent!.childCount > 1)
            Object.DestroyImmediate(AvatarsListContent.GetChild(1).gameObject);
        Button templateButton = AvatarsListContent.GetChild(0).GetComponent<Button>();
        RectTransform templateButtonRect = templateButton.gameObject.GetComponent<RectTransform>();
        string[] files = Directory.GetFiles(Plugin.AvatarsPath);
        float sizes = templateButtonRect.anchoredPosition3D.y;
        for (int i = 0; i < files.Length; i++)
        {
            string fileLocation = files[i].Replace('\\', '/');
            if(!Path.GetExtension(fileLocation).Contains("lca")) continue;
            Button newButton = Object.Instantiate(templateButton.gameObject, AvatarsListContent).GetComponent<Button>();
            Avatar? avatar = PlayerAvatarAPI.LoadAvatar(fileLocation);
            if (avatar == null)
            {
                Plugin.PluginLogger.LogError($"Failed to load AssetBundle at {files[i]}");
                continue;
            }
            TMP_Text newButtonText = newButton.transform.GetChild(0).GetComponent<TMP_Text>();
            newButtonText.text = avatar != null ? avatar.AvatarName : String.Empty;
            newButtonText.font = font;
            newButton.onClick.AddListener(() => CurrentlySelectedAvatar = (fileLocation, avatar));
            RectTransform newButtonRect = newButton.GetComponent<RectTransform>();
            if (i > 0)
            {
                sizes -= newButton.GetComponent<RectTransform>().rect.height;
                sizes -= 5;
            }
            newButtonRect.anchoredPosition3D = new Vector3(templateButtonRect.anchoredPosition3D.x, sizes,
                templateButtonRect.anchoredPosition3D.z);
            newButton.gameObject.SetActive(true);
        }
    }
    
    internal static void Prepare(Scene mainScene)
    {
        using Stream? s = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("LethalAvatars.GameUI.lethalavatars");
        if (s == null)
        {
            Plugin.PluginLogger.LogError("Failed to load AssetBundle! No in-game UI will be available.");
            return;
        }
        // Steal stuff
        Transform mainButtonsReference = mainScene.GetRootGameObjects().First(x => x.name == "Canvas").transform
            .Find("MenuContainer/MainButtons");
        GameObject newButtonGameObject = new GameObject("CustomAvatars");
        newButtonGameObject.transform.SetParent(mainButtonsReference);
        Transform hostButtonReference = mainButtonsReference.Find("HostButton");
        TMP_Text hostButtonText = hostButtonReference.GetChild(1).GetComponent<TMP_Text>();
        font = hostButtonText.font;
        newButtonGameObject.transform.localPosition = new Vector3(90, -56.2465f, -1.2f);
        newButtonGameObject.transform.localRotation = hostButtonReference.localRotation;
        newButtonGameObject.transform.localScale = hostButtonReference.localScale;
        Button newButton = newButtonGameObject.AddComponent<Button>();
        newButtonGameObject.name = "CustomAvatars";
        newButton.onClick.AddListener(() =>
        {
            RefreshAvatars();
            MainCanvas!.gameObject.SetActive(true);
        });
        Transform newButtonTransform = newButton.transform;
        GameObject newButtonTextGameObject = new GameObject("Text (TMP)");
        newButtonTextGameObject.transform.SetParent(newButtonTransform);
        newButtonTextGameObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        newButtonTextGameObject.transform.localScale = Vector3.one;
        TMP_Text newButtonText = newButtonTextGameObject.AddComponent<TextMeshProUGUI>();
        newButtonText.text = "> Custom Avatars";
        newButtonText.fontSize = 19;
        newButtonText.color = new Color(253/255f, 115/255f, 0, 1);
        newButtonText.font = font;
        // Load the AssetBundle
        AssetBundle assetBundle = AssetBundle.LoadFromStream(s);
        GameObject[] gameObjects = assetBundle.LoadAllAssets<GameObject>();
        MainCanvas = gameObjects.First(x => x.name == "LethalAvatarsUI").GetComponent<Canvas>();
        LoadingNameplate.ProgressCanvas =
            gameObjects.First(x => x.name == "LethalAvatarsProgress").GetComponent<Canvas>();
        MainCanvas.gameObject.SetActive(false);
        MainCanvas = Object.Instantiate(MainCanvas);
        SceneManager.MoveGameObjectToScene(MainCanvas.gameObject, mainScene);
        MainCanvas.gameObject.name = "LethalAvatarsUI";
        MainCanvas.gameObject.SetActive(false);
        Bcg = MainCanvas.transform.GetChild(0);
        AvatarsListContent = Bcg.GetChild(0).GetChild(0).GetChild(0);
        SelectedAvatar = Bcg.GetChild(1);
        AvatarNameText = SelectedAvatar.GetChild(0).GetComponent<TMP_Text>();
        AvatarCreatorText = SelectedAvatar.GetChild(1).GetComponent<TMP_Text>();
        AvatarIcon = SelectedAvatar.GetChild(2).GetComponent<Image>();
        AvatarDescriptionText = SelectedAvatar.GetChild(3).GetComponent<TMP_Text>();
        AvatarNameText.font = font;
        AvatarCreatorText.font = font;
        AvatarDescriptionText.font = font;
        Transform equipButton = SelectedAvatar.GetChild(4);
        equipButton.GetComponent<Button>().onClick.AddListener(() =>
        {
            if(string.IsNullOrEmpty(CurrentlySelectedAvatar.Item1)) return;
            Plugin.SelectedAvatar = CurrentlySelectedAvatar.Item1;
        });
        equipButton.GetChild(0).GetComponent<TMP_Text>().font = font;
        Transform backButton = Bcg.transform.GetChild(2);
        backButton.GetComponent<Button>().onClick.AddListener(() =>
        {
            CurrentlySelectedAvatar = (String.Empty, null);
            MainCanvas.gameObject.SetActive(false);
        });
        backButton.transform.GetChild(0).GetComponent<TMP_Text>().font = font;
        RefreshAvatars();
        assetBundle.Unload(false);
    }
}