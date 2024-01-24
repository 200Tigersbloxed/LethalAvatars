using System;
using UnityEditor.PackageManager;

namespace LethalAvatars.SDK.Editor.Internals;

internal static class PackageInstaller
{
    public static Action OnLoaded = () => { };

    static PackageInstaller() => Events.registeredPackages += _ => OnLoaded.Invoke();
    
    public static void AddPackage(string packageName) => Client.Add(packageName);
}