using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility;

public class WineSettings
{
    public bool IsManaged => !string.IsNullOrEmpty(DownloadUrl);

    public bool IsProton { get; private set; }

    public bool IsUsingContainer => File.Exists(ContainerPath);

    public string WineServerPath { get; private set; }

    public string WinePath { get; private set; }

    private string BinPath;

    public string DownloadUrl;

    public string ContainerPath { get; private set; }

    public bool EsyncOn { get; private set; }

    public bool FsyncOn { get; private set; }

    public string DebugVars { get; private set; }

    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    public string Runner => string.IsNullOrEmpty(ContainerPath) ? WinePath : ContainerPath;

    public string QuickRunner => WinePath;

    public string QuickRunArgs => IsProton ? "run " : "";

    public string QuickRunInPrefixArgs => IsProton ? "runinprefix " : "";

    public WineSettings(bool isProton, string wineFolder, string downloadUrl, string containerPath, DirectoryInfo storageFolder, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
    {
        // storageFolder is the path to .xlcore folder. managedFolder is the foldername inside the tarball that will be downloaded from managedUrl.
        IsProton = isProton;
        BinPath = isProton ? wineFolder : Path.Combine(wineFolder, "bin");
        WinePath = isProton ? Path.Combine(BinPath, "proton") : File.Exists(Path.Combine(BinPath, "wine64")) ? Path.Combine(BinPath, "wine64") : Path.Combine(BinPath, "wine");
        WineServerPath = isProton ? Path.Combine(BinPath, "files", "bin", "wineserver") : Path.Combine(BinPath, "wineserver");
        DownloadUrl = downloadUrl;
        ContainerPath = containerPath;

        this.EsyncOn = esyncOn ?? false;
        this.FsyncOn = fsyncOn ?? false;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
    }
}