using System;
using Indieteur.SAMAPI;

namespace LethalAvatars.SDK.Editor.Internals;

internal static class SteamHelper
{
    private const int STEAM_GAME_ID = 1966720;
        
    private static string GetSteamLocation()
    {
        string sl = SteamAppsManager.GetSteamDirectory();
        if (!string.IsNullOrEmpty(sl))
            return sl;
        return String.Empty;
    }
    
    public static string GetGameLocation()
    {
        string pathToSteam = GetSteamLocation();
        if(!string.IsNullOrEmpty(pathToSteam))
        {
            SteamAppsManager steamAppsManager = new SteamAppsManager(pathToSteam);
            SteamApp game = null;
            foreach (SteamApp steamApp in steamAppsManager.SteamApps)
            {
                if (steamApp.AppID != STEAM_GAME_ID) continue;
                game = steamApp;
                break;
            }

            if (game != null)
                return game.InstallDir;
        }
        return String.Empty;
    }
}