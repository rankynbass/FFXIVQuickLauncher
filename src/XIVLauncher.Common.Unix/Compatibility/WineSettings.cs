﻿using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility;

/*
To successfully launch dalamud/ffxiv in steam linux runtime container with proton, the following environtment variables *must* be set:

    STEAM_COMPAT_DATA_PATH, which is the proton prefix. The *wine* prefix is STEAM_COMPAT_DATA_PATH/pfx, which we symlink back to STEAM_COMPAT_DATA_PATH
    We do *not* need to set WINEPREFIX directly; Proton will take care of that.
    
    STEAM_COMPAT_CLIENT_INSTALL_PATH, which is the location of the steam install in the user's profile. Usually $HOME/.local/share/Steam. If this is not set
    during prefix creation/refresh, the resulting prefix will be *broken*, and it will be impossible to launch programs using proton.

These environment variables are needed in some cases:

    STEAM_COMPAT_MOUNTS, a colon separated list of directories to be allowed inside the container. If ffxiv or ffxivConfig are not in the user's home folder,
    this needs to include those paths or they wont be visible from the container. This is basically the Steam equivalent of flatseal file permissions.
    You *cannot* pass certain directories. /bin, /usr/bin, /lib, /usr/lib, /run, /run/user/<userid>, /proc, and a few others can't be passed. You *can* pass
    specific files inside those directories, however.
    
    STEAM_COMPAT_INSTALL_PATH is supposed to point to the game's install directory. However, we're just using STEAM_COMPAT_MOUNTS for everything instead,
    because if we launch from Steam, this might automatically get set for us.

    PRESSURE_VESSEL_FILESYSTEMS_RW is another way to pass files and directories into the container. Again, we're just using STEAM_COMPAT_MOUNTS.

    WINEDLLOVERRIDES to specify that dxvk should be used, just like with wine. This is supposed to be set by proton, but it doesn't work properly, so we set
    it manually.
    
    PROTON_NO_FSYNC and PROTON_NO_ESYNC need to be set if esync/fsync are disabled. These options are exactly opposite of the wine equivalents, WINEESYNC
    and WINEFSYNC. Proton will set WINEESYNC and WINEFSYNC for us. These need to be set during EnsurePrefix.

The actual command that gets run looks like this:

    "$HOME/.local/share/Steam/steamapps/common/SteamLinuxRuntime_sniper/_v2-entry-point" --verb=waitforexitandrun -- "$HOME/.local/share/Steam/compatibilitytools.d/GE-Proton8-9/proton" runinprefix <command>

Without using the runtime, it looks like this:

    "$HOME/.local/share/Steam/compatibilitytools.d/GE-Proton8-9/proton" runinprefix <command>

When ensuring the prefix, we use "run" instead of "runinprefix" because the first command will make sure the prefix is set up before returning, while the
second assumes the prefix is already set up.

Proton has built-in commands for doing unix->windows path converions. We don't use it because it is just a wrapper for the equivalent wine commands, and it
uses 32-bit wine when we can't assume 32-bit support will be available (for example in flatpak releases).
*/

public class WineSettings
{
    public bool IsProton { get; private set; }

    public bool IsRuntime => !string.IsNullOrEmpty(RuntimePath);

    public string WineServerPath { get; private set; }

    public string WinePath { get; private set; }

    private string BinPath;

    public string DownloadUrl;

    private string RuntimePath;

    public string RuntimeUrl;

    public bool EsyncOn { get; private set; }

    public bool FsyncOn { get; private set; }

    public string DebugVars { get; private set; }

    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    // Runner will be the steam linux runtime if used, or wine/proton if not used.
    public string Runner => string.IsNullOrEmpty(RuntimePath) ? WinePath : RuntimePath;

    // Run and RunInPrefix will be set to the appropriate values if using proton (with or without container), or "" if using wine.
    public string Run => IsProton ? "run " : "";

    public string RunInPrefix => IsProton ? "runinprefix " : "";

    // RunInRuntime and RunInRuntimeArray will be used if we're using a container. Otherwise set to "".
    public string RunInRuntime => IsRuntime ? $"--verb=waitforexitandrun -- \"{WinePath}\" " : "";

    public string[] RunInRuntimeArray => IsRuntime ? new string[] {"--verb=waitforexitandrun", "--", WinePath} : new string[] { };

    /*  
        The end result of the above variables is that we will build the process commands as follows:
        Process: Runner
        Arguements: RunInRuntime + WinePath + Run/RunInPrefix + command.

        If wine, that'll look like: /path/to/wine64 command
        If proton, it'll look like: /path/to/proton runinprefix command
        If steam runtime, it'll be: /path/to/runtime --verb=waitforexitandrun -- /path/to/proton runinprefix command
    */

    public WineSettings(bool isProton, string wineFolder, string downloadUrl, string runtimePath, string runtimeUrl, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
    {
        IsProton = isProton;
        BinPath = isProton ? wineFolder : WineCheck(wineFolder);
        WinePath = isProton ? Path.Combine(BinPath, "proton") : File.Exists(Path.Combine(BinPath, "wine64")) ? Path.Combine(BinPath, "wine64") : Path.Combine(BinPath, "wine");
        WineServerPath = isProton ? Path.Combine(BinPath, "files", "bin", "wineserver") : Path.Combine(BinPath, "wineserver");
        DownloadUrl = downloadUrl;
        RuntimePath = string.IsNullOrEmpty(runtimePath) ? "" : Path.Combine(runtimePath, "_v2-entry-point");
        RuntimeUrl = runtimeUrl;

        this.EsyncOn = esyncOn ?? false;
        this.FsyncOn = fsyncOn ?? false;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
    }

    private string WineCheck(string dir)
    {
        var directory = new DirectoryInfo(dir);
        if (directory.Name == "bin")
            return directory.FullName;
        return Path.Combine(directory.FullName, "bin");            
    }
}