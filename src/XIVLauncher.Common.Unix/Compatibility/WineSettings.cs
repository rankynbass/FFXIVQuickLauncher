using System;
using System.IO;
using System.Collections;

namespace XIVLauncher.Common.Unix.Compatibility;

public enum WineStartupType
{
    [SettingsDescription("Proton (requires Steam)", "Use a Proton installation, which includes DXVK and other features built in.")]
    Proton,

    [SettingsDescription("Managed by XIVLauncher", "The game installation and wine setup is managed by XIVLauncher - you can leave it up to us.")]
    Managed,

    [SettingsDescription("Official Wine-XIV 7.10 (Official Launcher Default)", "A custom version of Wine-TKG 7.10 with XIV patches.")]
    Official7_10,

    [SettingsDescription("Official Wine-XIV 8.5 (New Default)", "A custom version of Wine-TKG 8.5 with XIV patches. Includes stutter fix.")]
    Official8_5,

    [SettingsDescription("Unofficial Wine-XIV 8.8", "A custom version of Wine-TKG 8.8.r7 with XIV patches. Includes stutter fix.")]
    Unofficial8_8,    

    [SettingsDescription("RB's Wine Proton7-35", "Based on Wine-GE, but with XIV and Haptic Feedback patches applied.")]
    Proton7_35,

    [SettingsDescription("RB's Wine Proton7-43", "Based on Wine-GE, but with XIV and Haptic Feedback patches applied. Includes stutter fix.")]
    Proton7_43,

    [SettingsDescription("RB's Wine Proton8-4", "Based on Wine-GE, but with XIV patches applied. Includes stutter fix. No FSR.")]
    Proton8_4,

    [SettingsDescription("RB's Wine Proton8-7", "Based on Wine-GE, but with XIV patches applied. Includes stutter fix. No FSR.")]
    Proton8_7,

    [SettingsDescription("Custom", "Point XIVLauncher to a custom location containing wine binaries to run the game with.")]
    Custom,
}

public class WineSettings
{
    public WineStartupType StartupType { get; private set; }
    public string CustomBinPath { get; private set; }

    public bool EsyncOn { get; private set; }
    public bool FsyncOn { get; private set; }

    public string DebugVars { get; private set; }
    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    public string WineFolder { get; private set; }

    public string WineURL { get; private set; }

#if WINE_XIV_ARCH_LINUX
    private const string DISTRO = "arch";
#elif WINE_XIV_FEDORA_LINUX
    private const string DISTRO = "fedora";
#else
    private const string DISTRO = "ubuntu";
#endif

    public WineSettings(WineStartupType? startupType, string customBinPath, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
    {
        this.StartupType = startupType ?? WineStartupType.Custom;
        this.CustomBinPath = customBinPath;
        this.EsyncOn = esyncOn ?? false;
        this.FsyncOn = fsyncOn ?? false;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;

        switch (StartupType)
        {
            case WineStartupType.Official7_10:
                WineURL = $"https://github.com/goatcorp/wine-xiv-git/releases/download/7.10.r3.g560db77d/wine-xiv-staging-fsync-git-{DISTRO}-7.10.r3.g560db77d.tar.xz";
                WineFolder = "wine-xiv-staging-fsync-git-7.10.r3.g560db77d";
                break;
            
            case WineStartupType.Managed:
            case WineStartupType.Official8_5:
                WineURL = $"https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-{DISTRO}-8.5.r4.g4211bac7.tar.xz";
                WineFolder = "wine-xiv-staging-fsync-git-8.5.r4.g4211bac7";
                break;

            case WineStartupType.Unofficial8_8:
                WineURL = "https://github.com/rankynbass/unofficial-wine-xiv-git/releases/download/v8.8.0/unofficial-wine-xiv-git-8.8.0.tar.xz";
                WineFolder = "unofficial-wine-xiv-git-8.8.0";
                break;

            case WineStartupType.Proton7_35:
                WineURL = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton7-35/unofficial-wine-xiv-Proton7-35-x86_64.tar.xz";
                WineFolder = "unofficial-wine-xiv-Proton7-35-x86_64";
                break;

            case WineStartupType.Proton7_43:
                WineURL = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton7-43/unofficial-wine-xiv-Proton7-43-x86_64.tar.xz";
                WineFolder = "unofficial-wine-xiv-Proton7-43-x86_64";
                break;

            case WineStartupType.Proton8_4:
                WineURL = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-4/unofficial-wine-xiv-Proton8-4-x86_64.tar.xz";
                WineFolder = "unofficial-wine-xiv-Proton8-4-x86_64";
                break;

            case WineStartupType.Proton8_7:
                WineURL = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton8-7/unofficial-wine-xiv-Proton8-7-x86_64.tar.xz";
                WineFolder = "unofficial-wine-xiv-Proton8-7-x86_64";
                break;

            case WineStartupType.Proton:
            case WineStartupType.Custom:
                WineURL = "";
                WineFolder = "";
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}