# LethalAvatars
A Lethal Company mod that allows you to export Avatars directly from Unity

![banner](https://github.com/200Tigersbloxed/LethalAvatars/assets/45884377/763ab7a5-4a54-4c9f-9289-6dc536b6f16d)

To describe this mod very shortly, it allows you to express your creativity and show it off to your friends while playing your most favorite game ever! If you're familiar with the avatar creation process for games like VRChat, ChilloutVR, or [Hypernex](https://www.hypernex.dev/) (shameless plug), you'll be very familiar with this system.

> [!WARNING]
> This mod is in *early development* and is **VERY UNSTABLE**!

## How to Install the Mod

This mod uses [BepInEx v5.4.22](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.22).

Fortunately, the mod is simply one dll. No dependencies required! Just drag [`LethalAvatars.dll`](https://github.com/200Tigersbloxed/LethalAvatars/releases/download/0.1.0/LethalAvatars.dll) to `path/to/Lethal Comnpany/BepInEx/plugins` folder and done!

## Installing an Avatar

All Avatar formats are classified as `.lca` files (**L**ethal **C**ompany **A**vatar) and, at heart, are just Unity AssetBundles (again, just like Hypernex. seeing the pattern?) All avatars can be installed by moving the avatar file to `path/to/Lethal Company/Avatars` folder (if the Avatars folder does not exist, run the game once and it will be created). At runtime, the avatars will be loaded and displayed in your Custom Avatars menu on the home screen.

![image](https://github.com/200Tigersbloxed/LethalAvatars/assets/45884377/4bb9586c-54b1-4214-b296-a48ad79b958e)

![image](https://github.com/200Tigersbloxed/LethalAvatars/assets/45884377/71cf3950-4a89-4283-a4d3-6447e4c4011f)

## Creating an Avatar

The first step to creating an avatar is using the correct Unity version. Lethal Company uses [2022.3.9f1](https://unity.com/releases/editor/whats-new/2022.3.9), and you should use this (or at least some other 2022.3.XX build).

Next, import the [`LethalCompany.SDK.unitypackage`](https://github.com/200Tigersbloxed/LethalAvatars/releases/download/0.1.0/LethalCompany.SDK.unitypackage) file. 

Finally, build with the `LethalAvatars > Build` window.

See the [Wiki](https://github.com/200Tigersbloxed/LethalAvatars/wiki) for more information on how to create an avatar.

## Uploading an Avatar

You don't have to! All avatars are synchronized in-game, even if the players don't have the asset!

> [!WARNING]  
> This will send your avatar's AssetBundles to other clients, meaning that clients *could* extract the binary data. You should only play with friends who you trust.
> Avatars gathered over the network will **never** save to storage.

### Distributing an Avatar

Currently, there is no service for distributing avatars. While you could always upload a mod that injects avatars into the Avatars folder, then upload that to Thunderstore, that is really clunky and disorganized. I would be more than happy to open a channel for distributing Lethal Company avatars [in my discord server](https://fortnite.lol/discord). If anyone has suggestions for platforms, I am also open for that too!

# Building

For you tech-savvy nerds who don't want pre-builts or want to contribute. (no shame, shoutout to all the dorks out there)

1. Pull the code, open the solution in your preferred IDE, and import NuGet packages
2. Create a file in the LethalAvatars project called `LethalAvatars.csproj.user`, and set the text to the following
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <PropertyGroup>
        <LethalCompanyPath>path/to/Lethal Company</LethalCompanyPath>
    </PropertyGroup>
</Project>
```
  + Replace `path/to/Lethal Company` with your Lethal Company directory
3. Create a file in the LethalAvatars.SDK project called `LethalAvatars.SDK.csproj.user`, and set the text to the following
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <PropertyGroup>
        <LethalCompanyPath>path/to/Lethal Company</LethalCompanyPath>
    </PropertyGroup>
</Project>
```
  + Replace `path/to/Lethal Company` with your Lethal Company directory
4. Create a file in the LethalAvatars.SDK.Editor project called `LethalAvatars.SDK.Editor.csproj.user` and set the text to the following
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <UnityEditorManagedPath>path/to/Unity 2022.3.9f1/Editor/Data/Managed</UnityEditorManagedPath>
  </PropertyGroup>
</Project>
```
  + Replace `path/to/Unity 2022.3.9f1` with your Unity install directory
5. Reload all the projects and Build

## Why is building like this?

LethalAvatars requires dependencies that are not present in Lethal Company. Lethal Company relies on [protobuf-net](https://github.com/protobuf-net/protobuf-net) for message serialization/deserialization. This is used because LethalAvatars relies on low-level Unity Netcode APIs, which unfortunately, are barely documented (hours of my life, wasted) for the sole purpose of relaying Avatar data over the network. You can read more information about that process [here](https://forum.unity.com/threads/issue-sending-dynamic-data-possibly-bad-design-on-my-part-reading-past-end-of-buffer-error.1215117/#post-7834923).

Dependencies are linked using EmbeddedResources and the [AppDomain.AssemblyResolve](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.assemblyresolve?view=netframework-4.8) event. Developing in early stages, I found out that when using ILRepack to combine assemblies (such as LethalAvatars.SDK, a core requirement for ALL of LethalAvatars) that when the assembly changed, Unity would not be able to resolve it. This means that every time there was a slight change in the LethalAvatars.SDK assembly, Unity would not be able to resolve the components attached to an Avatar. Resolving assemblies from memory is the exact same as if the assembly were to be loaded by Unity itself, just that a user would not have to manually install the dependency as well. To better understand how libraries are linked together, here's a diagram I drew up.

![jhsdfbgsjhdbg](https://github.com/200Tigersbloxed/LethalAvatars/assets/45884377/7335312f-7309-470d-aa7e-d651101da3bf)
