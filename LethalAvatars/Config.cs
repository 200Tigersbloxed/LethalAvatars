using System;
using System.IO;
using Tomlet;
using Tomlet.Attributes;
using Tomlet.Models;

namespace LethalAvatars;

internal class Config
{
    [TomlProperty("SelectedAvatar")]
    [TomlPrecedingComment("The current equipped avatar")]
    public string SelectedAvatar { get; set; } = String.Empty;

    public void Save(string fileLocation)
    {
        TomlDocument d = TomletMain.DocumentFrom(this);
        File.WriteAllText(fileLocation, d.SerializedValue);
    }

    public static Config Create(string fileLocation)
    {
        if (!File.Exists(fileLocation))
        {
            Config c = new Config();
            c.Save(fileLocation);
            return c;
        }
        try
        {
            Config cc = TomletMain.To<Config>(File.ReadAllText(fileLocation));
            return cc;
        }
        catch (Exception)
        {
            Plugin.PluginLogger.LogWarning("Invalid Config! All values will now be reset. Sorry!");
            Config c = new Config();
            c.Save(fileLocation);
            return c;
        }
    }
}