using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using LethalAvatars.Libs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LethalAvatars.GameUI;

internal class LoadingNameplate
{
    internal static Canvas? ProgressCanvas;

    private static Dictionary<string, NameplateDriver> nameplates = new();

    internal static void Apply(PlayerControllerB player, float value)
    {
        NameplateDriver? nameplateDriver;
        if (!nameplates.TryGetValue(player.GetIdentifier(), out nameplateDriver))
            nameplateDriver = ApplyLoadingToAvatar(player);
        if(nameplateDriver == null) return;
        nameplateDriver.Apply(value);
    }

    internal static void Finish(PlayerControllerB player)
    {
        if (!nameplates.TryGetValue(player.GetIdentifier(), out NameplateDriver nameplateDriver)) return;
        nameplateDriver.Dispose();
        nameplates.Remove(player.GetIdentifier());
    }

    internal static void Update()
    {
        foreach (KeyValuePair<string,NameplateDriver> nameplateDriver in new Dictionary<string, NameplateDriver>(nameplates))
        {
            if (nameplateDriver.Value._Update()) continue;
            nameplateDriver.Value.Dispose();
            nameplates.Remove(nameplateDriver.Key);
        }
    }
    
    private static NameplateDriver? ApplyLoadingToAvatar(PlayerControllerB player)
    {
        PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
        if (localPlayer == null) return null;
        if (player.GetIdentifier() == localPlayer.GetIdentifier()) return null;
        Transform nameplateReference = player.transform.Find("PlayerNameCanvas");
        for (int i = 0; i < player.transform.childCount; i++)
        {
            Transform child = player.transform.GetChild(i);
            if (child.gameObject.name.Contains("PlayerNameCanvas") ||
                child.gameObject.name.Contains("PlayerUsernameCanvas"))
            {
                nameplateReference = child;
                break;
            }
        }
        if (nameplateReference == null) return null;
        Canvas newCanvas = Object.Instantiate(ProgressCanvas!.gameObject).GetComponent<Canvas>();
        NameplateDriver nameplateDriver = new NameplateDriver(newCanvas, nameplateReference,
            localPlayer.transform.Find("ScavengerModel/metarig/CameraContainer/MainCamera"));
        newCanvas.transform.SetParent(player.transform);
        newCanvas.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        nameplates.Add(player.GetIdentifier(), nameplateDriver);
        return nameplateDriver;
    }

    private class NameplateDriver : IDisposable
    {
        public Canvas Nameplate;
        public Transform Reference;
        public Transform Camera;
        private Slider slider;
        private TMP_Text text;

        public NameplateDriver(Canvas nameplate, Transform reference, Transform camera)
        {
            Nameplate = nameplate;
            Reference = reference;
            Camera = camera;
            slider = nameplate.transform.Find("Slider").GetComponent<Slider>();
            text = slider.transform.Find("Fill Area/Fill/Progress").GetComponent<TMP_Text>();
        }

        public void Apply(float percent)
        {
            slider.value = percent;
            text.text = Math.Round(percent * 100, 2).ToString("0.00") + "%";
        }

        public bool _Update()
        {
            if (Nameplate == null || Reference == null || Camera == null)
                return false;
            Nameplate.transform.position =
                new Vector3(Reference.position.x, Reference.position.y + 0.25f, Reference.position.z);
            Nameplate.transform.rotation =
                Quaternion.LookRotation((Nameplate.transform.position - Camera.transform.position).normalized);
            return true;
        }

        public void Dispose()
        {
            if(Nameplate == null) return;
            Object.DestroyImmediate(Nameplate.gameObject);
        }
    }
}