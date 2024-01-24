using System;
using System.IO;

namespace LethalAvatars.SDK.Editor.Internals;

internal class DirectoryIdentifier : IDisposable
{
    private string DirectoryPath;

    public DirectoryIdentifier(bool inAssets = true)
    {
        string dir = String.Empty;
        if (inAssets)
            dir += "Assets/";
        dir += Guid.NewGuid().ToString() + '/';
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        DirectoryPath = dir;
    }

    public string GetFilePath(string file) => Path.Combine(DirectoryPath, file);

    public string CreateSubDirectory(string pathToDir)
    {
        string dir = Path.Combine(DirectoryPath, pathToDir);
        if(!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    public void Dispose()
    {
        Directory.Delete(DirectoryPath, true);
        string meta = DirectoryPath.TrimEnd('/') + ".meta";
        if(File.Exists(meta))
            File.Delete(meta);
    }
}