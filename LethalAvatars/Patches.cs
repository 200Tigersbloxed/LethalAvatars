using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using LethalAvatars.Libs;
using LethalAvatars.Networking;
using LethalAvatars.Networking.Messages;
using UnityEngine;
using Avatar = LethalAvatars.SDK.Avatar;

namespace LethalAvatars;

class Patches
{
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.SpawnPlayerAnimation), new Type[0])]
    class PlayerControllerBSpawn
    {
        static void Postfix(ref PlayerControllerB __instance)
        {
            if(Plugin.joinedRound) return;
            Plugin.joinedRound = true;
            NetworkHandler.connectedPlayers = PlayerAvatarAPI.GetAllPlayers().ToList();
            // Check if an avatar already exists
            if (PlayerAvatarAPI.RegisteredAvatars.ContainsKey(__instance))
            {
                Plugin.PluginLogger.LogInfo("LocalPlayer already has an avatar loaded. Removing.");
                PlayerAvatarAPI.ResetPlayer(__instance);
            }
            // Check if the file exists
            if (File.Exists(Plugin.SelectedAvatar))
            {
                Avatar? avatar = PlayerAvatarAPI.LoadAvatar(Plugin.SelectedAvatar);
                if (avatar == null)
                {
                    Plugin.PluginLogger.LogError($"Failed to load avatar at {Plugin.SelectedAvatar}");
                    if (__instance != null)
                        PlayerAvatarAPI.ResetPlayer(__instance);
                }
                else
                {
                    PlayerAvatarAPI.ApplyNewAvatar(avatar, __instance, Extensions.GetHashOfFile(Plugin.SelectedAvatar));
                    SwitchAvatar switchAvatarMessage = new SwitchAvatar
                    {
                        AvatarFileHash = Extensions.GetHashOfFile(Plugin.SelectedAvatar)
                    };
                    switchAvatarMessage.Broadcast();
                }
            }
            else
                Plugin.PluginLogger.LogWarning($"Could not find avatar at {Plugin.SelectedAvatar}");
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "BeginGrabObject", new Type[0])]
    class PlayerControllerBBeginGrab
    {
        static void Postfix(PlayerControllerB __instance)
        {
            if(!PlayerAvatarAPI.RegisteredAvatars.TryGetValue(__instance, out Avatar avatar)) return;
            __instance.localItemHolder = __instance.twoHanded ? avatar.SmallItemGrab :
                avatar.BigItemGrab == null ? avatar.SmallItemGrab : avatar.BigItemGrab;
        }
    }

    private static List<ulong> sentClients = new();
    
    [HarmonyPatch(typeof(StartOfRound), "OnPlayerConnectedClientRpc",
        new[]
        {
            typeof(ulong), typeof(int), typeof(ulong[]), typeof(int), typeof(int), typeof(int), typeof(int),
            typeof(int), typeof(int), typeof(int), typeof(bool)
        })]
    class StartOfRoundPatchA
    {
        static void Postfix(ulong clientId, int connectedPlayers, ulong[] connectedPlayerIdsOrdered,
            int assignedPlayerObjectId, int serverMoneyAmount, int levelID, int profitQuota, int timeUntilDeadline,
            int quotaFulfilled, int randomSeed, bool isChallenge)
        {
            if(sentClients.Contains(clientId)) return;
            NetworkHandler.connectedPlayers = PlayerAvatarAPI.GetAllPlayers().ToList();
            NetworkHandler.connectedPlayers.ForEach(x =>
            {
                if(sentClients.Contains(x.GetNetworkIdentifier())) return;
                LethalAvatarsRunner.Instance!.RunCoroutine(WaitUntilSend(x));
            });
            sentClients.Add(clientId);
        }

        static IEnumerator WaitUntilSend(PlayerControllerB player)
        {
            yield return new WaitForSeconds(5f);
            if(File.Exists(Plugin.SelectedAvatar))
            {
                // Send the current avatar information to the newly connected client
                string hash = Extensions.GetHashOfFile(Plugin.SelectedAvatar);
                SwitchAvatar switchAvatarMessage = new SwitchAvatar
                {
                    AvatarFileHash = hash
                };
                switchAvatarMessage.Send(player);
            }
        }
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ReviveDeadPlayers), new Type[0])]
    class StartOfRoundPatchB
    {
        static void Postfix() => PlayerAvatarAPI.RefreshAllAvatars();
    }

    [HarmonyPatch(typeof(GameNetworkManager), "ResetGameValuesToDefault", new Type[0])]
    class GameNetworkManagerPatch
    {
        static void Prefix()
        {
            sentClients.Clear();
            PlayerAvatarAPI.Reset();
        }
    }

    [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SetLevelOfPlayer),
        new Type[3] {typeof(PlayerControllerB), typeof(int), typeof(bool)})]
    class HUDManagerPatch
    {
        static void Postfix(PlayerControllerB playerScript, int playerLevelIndex, bool hasBeta)
        {
            PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
            if(localPlayer == null || !playerScript.IsLocal()) return;
            if(!PlayerAvatarAPI.RegisteredAvatars.TryGetValue(playerScript, out Avatar avatar)) return;
            AvatarDriver avatarDriver = avatar.GetComponent<AvatarDriver>();
            if(avatarDriver == null) return;
            avatarDriver.IncludeParameters["Level"] = playerLevelIndex;
            avatarDriver.IncludeParameters["HasBeta"] = hasBeta;
        }
    }

    [HarmonyPatch(typeof(StartMatchLever), nameof(StartMatchLever.LeverAnimation), new Type[0])]
    class StartMatchLeverPath
    {
        static void Postfix(StartMatchLever __instance)
        {
            // Same game logic
            if (GameNetworkManager.Instance.localPlayerController.isPlayerDead ||
                __instance.playersManager.travellingToNewLevel || __instance.playersManager.inShipPhase &&
                __instance.playersManager.connectedPlayersAmount + 1 <= 1 && !__instance.singlePlayerEnabled)
                return;
            PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
            if(localPlayer == null) return;
            if(!PlayerAvatarAPI.RegisteredAvatars.TryGetValue(localPlayer, out Avatar avatar)) return;
            AvatarDriver avatarDriver = avatar.GetComponent<AvatarDriver>();
            if(avatarDriver == null) return;
            avatarDriver.IncludeParameters["PullLever"] = true;
            LethalAvatarsRunner.Instance!.RunCoroutine(EndPullLever(avatarDriver));
        }
        
        static IEnumerator EndPullLever(AvatarDriver avatarDriver)
        {
            yield return new WaitForSeconds(2f);
            avatarDriver.IncludeParameters["PullLever"] = false;
        }
    }
}