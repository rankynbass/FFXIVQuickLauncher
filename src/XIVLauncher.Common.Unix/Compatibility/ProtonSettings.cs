#nullable enable
using System.IO;
using System.Collections.Generic;


namespace XIVLauncher.Common.Unix.Compatibility;

public class ProtonSettings
{
    public string SteamPath { get; private set; }

    public string ProtonPath { get; private set; }

    public string RuntimePath { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    public List<string>? SteamCompatMounts { get; private set; }

    public ProtonSettings(string? steamPath, string protonPath, string runtimePath, DirectoryInfo prefix, List<string>? compatMounts)
    {
        // none of these should ever actually be null, but this stops the editor and compiler from complaining.
        var home = System.Environment.GetEnvironmentVariable("HOME") ?? "";
        SteamPath = steamPath ?? Path.Combine(home, ".local", "share", "Steam");
        ProtonPath = protonPath;
        RuntimePath = runtimePath;
        Prefix = prefix;
        SteamCompatMounts = compatMounts;
    }
}
#nullable restore