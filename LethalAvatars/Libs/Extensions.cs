using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using GameNetcodeStuff;
using LethalAvatars.Networking.Messages;
using LethalAvatars.SDK;
using Unity.Netcode;
using UnityEngine;

namespace LethalAvatars.Libs;

internal static class Extensions
{
    private const int MAX_CHUNK_SIZE = 1024;
    internal static Dictionary<string, List<byte[]>> chunkedAvatarData = new();
    
    public static void SafeAdd<T1, T2>(this Dictionary<T1, T2> dictionary, T1 key, T2 value, bool overwrite = true)
    {
        if (dictionary.ContainsKey(key))
        {
            if(!overwrite) return;
            dictionary[key] = value;
        }
        dictionary.Add(key, value);
    }
    
    internal static string GetHashOfFile(string fileLocation)
    {
        FileStream fileStream = new FileStream(fileLocation, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using MD5 md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(fileStream);
        string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        fileStream.Dispose();
        return hashString;
    }
    
    internal static string GetHashOfData(byte[] data)
    {
        using MD5 md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(data);
        string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return hashString;
    }

    internal static byte[]? GetAvatarData(string fileHash)
    {
        string file = String.Empty;
        foreach (string assetBundleFile in Directory.GetFiles(Plugin.AvatarsPath))
        {
            string hash = GetHashOfFile(assetBundleFile);
            if(hash != fileHash) continue;
            file = assetBundleFile;
            break;
        }
        if (string.IsNullOrEmpty(file))
        {
            Plugin.PluginLogger.LogError($"Failed to find Avatar file for hash {fileHash}!");
            return null;
        }
        using FileStream fileStream =
            new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using MemoryStream ms = new MemoryStream();
        fileStream.CopyTo(ms);
        return ms.ToArray();
    }

    internal static void ChunkData(byte[] fileData, Action<List<byte[]>> onData)
    {
        string hash = GetHashOfData(fileData);
        if (chunkedAvatarData.TryGetValue(hash, out List<byte[]> data))
            onData.Invoke(data);
        List<byte[]> datas = new();
        List<byte> current = new();
        Plugin.PluginLogger.LogDebug("Chunking data");
        new Thread(() =>
        {
            for (int i = 0; i < fileData.Length; i++)
            {
                byte b = fileData[i];
                if (sizeof(byte) * current.Count > MAX_CHUNK_SIZE)
                {
                    datas.Add(current.ToArray());
                    current.Clear();
                }
                current.Add(b);
            }
            if(current.Count > 0)
                datas.Add(current.ToArray());
            current.Clear();
            LethalAvatarsRunner.Instance!.Enqueue(() =>
            {
                Plugin.PluginLogger.LogDebug($"Chunked data ({datas.Count} chunks)");
                if(!chunkedAvatarData.ContainsKey(hash))
                    chunkedAvatarData.Add(hash, datas);
                onData.Invoke(datas);
            });
        }).Start();
    }


    internal static void LoadFromMemory(PlayerControllerB player, byte[] combinedData)
    {
        // Remove current avatar
        if (PlayerAvatarAPI.RegisteredAvatars.ContainsKey(player))
            PlayerAvatarAPI.ResetPlayer(player);
        SDK.Avatar? avatar = PlayerAvatarAPI.LoadAvatar(combinedData);
        if (avatar == null)
        {
            Plugin.PluginLogger.LogError($"Failed to load avatar for {player.GetIdentifier()}");
            PlayerAvatarAPI.ResetPlayer(player);
            return;
        }
        if (avatar.AllowDownloading)
        {
            string hash = GetHashOfData(combinedData);
            bool containsHash = false;
            foreach (string f in Directory.GetFiles(Plugin.AvatarsPath))
            {
                string h = GetHashOfFile(f);
                if(h != hash) continue;
                containsHash = true;
                break;
            }
            if(!containsHash)
            {
                string name = NameTools.GetSafeName(avatar.gameObject.name.Replace("(clone)", String.Empty).Trim());
                int c = Directory.GetFiles(Plugin.AvatarsPath).Select(Path.GetFileName).Count(x => x == name + ".lca");
                if (c > 0)
                    name += c.ToString();
                FileStream fs = new FileStream(Path.Combine(Plugin.AvatarsPath, name + ".lca"), FileMode.CreateNew,
                    FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
                fs.Write(combinedData, 0 , combinedData.Length);
                fs.Dispose();
            }
        }
        PlayerAvatarAPI.ApplyNewAvatar(avatar, player, GetHashOfData(combinedData));
    }

    private static IEnumerator _SendInterval(PlayerControllerB player, AvatarData[] avatarDataResponses)
    {
        foreach (AvatarData avatarDataResponse in avatarDataResponses)
        {
            yield return new WaitForSeconds(0.0001f);
            // Stop updating if we aren't connected anymore
            if(!Plugin.joinedRound)
            {
                Plugin.PluginLogger.LogDebug("Broken");
                yield break;
            }
            avatarDataResponse.Send(player);
        }
    }

    internal static void SendInterval(PlayerControllerB player, AvatarData[] avatarDatas) =>
        LethalAvatarsRunner.Instance!.RunCoroutine(_SendInterval(player, avatarDatas));

    public static string GetIdentifier(this PlayerControllerB player) => player.playerUsername;
    public static ulong GetNetworkIdentifier(this PlayerControllerB player) => player.actualClientId;
    public static bool IsMaster(this PlayerControllerB player) => player.IsOwnedByServer;

    public static bool IsLocal(this PlayerControllerB player)
    {
        if (player == null) return false;
        PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
        if (localPlayer == null) return false;
        return player.GetIdentifier() == localPlayer.GetIdentifier();
    }
}