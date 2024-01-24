using System.IO;
using Tomlet;
using Tomlet.Attributes;
using Tomlet.Models;
using UnityEngine;

namespace LethalAvatars.SDK.Editor.Internals;

internal class Preferences
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "editorconfig.toml");
    
    [TomlProperty("OutputLocation")]
    public string OutputLocation { get; set; }

    public void Save()
    {
        TomlDocument t = TomletMain.DocumentFrom(this);
        File.WriteAllText(FilePath, t.SerializedValue);
    }

    public static Preferences Create()
    {
        if (!File.Exists(FilePath))
        {
            Preferences p = new Preferences();
            p.Save();
            return p;
        }
        string text = File.ReadAllText(FilePath);
        return TomletMain.To<Preferences>(text);
    }
}