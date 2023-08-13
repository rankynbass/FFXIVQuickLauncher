using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility;

public class ProtonSettings
{
    public string SteamPath { get; private set; }

    public string ProtonPath { get; private set; }

    public string RuntimePath { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    public DirectoryInfo GamePath { get; private set; }

    public DirectoryInfo GameConfigPath { get; private set; }

    public ProtonSettings(string? steamPath, string protonPath, string runtimePath, DirectoryInfo prefix, DirectoryInfo? gamePath, DirectoryInfo? gameConfig)
    {
        // none of these should ever actually be null, but this stops the editor and compiler from complaining.
        var home = System.Environment.GetEnvironmentVariable("HOME") ?? "";
        SteamPath = steamPath ?? Path.Combine(home, ".local", "share", "Steam");
        ProtonPath = protonPath;
        RuntimePath = runtimePath;
        Prefix = prefix;
        GamePath = gamePath ?? new DirectoryInfo(Path.Combine(home, ".xlcore", "ffxiv"));
        GameConfigPath = gameConfig ?? new DirectoryInfo(Path.Combine(home, ".xlcore", "ffxivConfig"));
    }
}