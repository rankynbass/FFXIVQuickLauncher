using System;
using System.Collections.Generic;
using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility;

public class ProtonSettings
{
    public DirectoryInfo Prefix { get; }
    
    public string SteamRoot { get; }

    public string ProtonPath { get; }

    public string SteamLibrary => Path.Combine(SteamRoot, "steamapps", "common");

    public string RuntimeRun => Path.Combine(SteamRuntime, "run");

    public string RuntimeInject => Path.Combine(SteamRuntime,"_v2-entry-point");

    public string GamePath { get; }

    public string ConfigPath { get; }

    public string SteamRuntime { get; }

    public string ReaperPath => Path.Combine(SteamRoot,"ubuntu12_32","reaper");

    public string CompatMounts => GamePath + ':' + ConfigPath;

    public string SteamAppId { get; }

    public bool UseRuntime { get; }

    public ProtonSettings(DirectoryInfo protonPrefix, string steamRoot, string protonPath, string gamePath = "", string configPath = "", string appId = "39210", string steamRuntime = "")
    {
        Prefix = protonPrefix;
        string xlcore = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".xlcore");
        SteamRoot = steamRoot;
        ProtonPath = Path.Combine(protonPath, "proton");
        GamePath = string.IsNullOrEmpty(gamePath) ? Path.Combine(xlcore, "ffxiv") : gamePath;
        ConfigPath = string.IsNullOrEmpty(configPath) ? Path.Combine(xlcore, "ffxivConfig") : configPath;
#if FLATPAK
        SteamRuntime = ""; // Already in a flatpak container, so this is ignored. Pressure-vessel and flatpak don't like to share.
#else
        SteamRuntime = steamRuntime;
#endif
        UseRuntime = !string.IsNullOrEmpty(SteamRuntime);
        SteamAppId = appId;
    }

    public string GetCommand(bool inject = true)
    {
#if FLATPAK
        inject = true;
#endif
        if (UseRuntime) return inject ? RuntimeInject : RuntimeRun;
        return ProtonPath;   
    }

    public string GetArguments(bool inject = true, string verb = "runinprefix")
    {
        List<string> commands = new List<string>();
        if (UseRuntime)
        {
            commands.Add(inject ? "--verb=waitforexitandrun --" : "--");
            commands.Add("\"" + ProtonPath + "\"");
        }
        commands.Add(verb);

        return string.Join(' ', commands);        
    }

    public string[] GetArgumentsAsArray(bool inject = true, string verb = "runinprefix")
    {
        List<string> commands = new List<string>();
        if (UseRuntime)
        {
            if (inject) commands.Add("--verb=waitforexitandrun");
            commands.Add("--");
            commands.Add(ProtonPath);
        }
        commands.Add(verb);

        return commands.ToArray();
    }
}