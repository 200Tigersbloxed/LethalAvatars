using System;
using System.IO;
using System.Linq;

namespace LethalAvatars.SDK;

public static class NameTools
{
    public static string GetSafeName(string rawAvatarName)
    {
        string avatarName = String.Empty;
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in rawAvatarName)
        {
            if(invalidChars.Contains(c)) continue;
            avatarName += c;
        }
        return avatarName;
    }
}