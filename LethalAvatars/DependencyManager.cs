using System;
using System.IO;
using System.Reflection;

namespace LethalAvatars;

internal class DependencyManager
{
    internal static void Initialize() => AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

    private static Assembly? ResolveAssembly(object sender, ResolveEventArgs args)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string location = $"LethalAvatars.Libs.{new AssemblyName(args.Name)}.dll";
        using Stream? stream = assembly.GetManifestResourceStream(location);
        // not our type
        if (stream == null) return null;
        byte[] data = new byte[stream.Length];
        int read = stream.Read(data, 0, data.Length);
        if (read != data.Length)
            throw new Exception($"Unexpected amount of bytes read! Read {read}, expected {data.Length}");
        return Assembly.Load(data);
    }
}