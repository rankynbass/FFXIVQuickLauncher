using System.IO;
using System.Collections.Generic;

namespace XIVLauncher.Common.Unix.Compatibility;

public class WineSettings
{
    public bool IsManaged { get; private set; }

    public bool IsProton { get; private set; }

    public string WineServerPath { get; private set; }

    public string WinePath { get; private set; }

    private string BinPath;

    public string AltWinePath { get; private set; }

    public string Arguments { get; private set; }

    public string RuntimePath { get; private set; }

    public string FolderName { get; private set; }

    public string DownloadUrl { get; private set; }

    public bool EsyncOn { get; private set; }

    public bool FsyncOn { get; private set; }

    public string DebugVars { get; private set; }

    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    public string SteamCompatMounts { get; private set; }

    public string ProtonPrefix { get; private set; }
    public string SteamPath { get; private set; }

    public WineSettings(bool isManaged, string customBinPath, string managedFolder, string managedUrl, DirectoryInfo storageFolder, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn, ProtonSettings? protonInfo)
    {
        // storageFolder is the path to .xlcore folder. managedFolder is the foldername inside the tarball that will be downloaded from managedUrl.
        IsManaged = isManaged;
        IsProton = protonInfo is not null;
        FolderName = managedFolder;
        DownloadUrl = managedUrl;
        BinPath = IsProton ? protonInfo.ProtonPath : (isManaged ? Path.Combine(storageFolder.FullName, "compatibilitytool", "wine", managedFolder, "bin") : customBinPath);
        WineServerPath = IsProton ? Path.Combine(BinPath, "files", "bin", "wineserver") : Path.Combine(BinPath, "wineserver");

        EsyncOn = esyncOn ?? false;
        FsyncOn = fsyncOn ?? false;
        DebugVars = debugVars;
        LogFile = logFile;
        Prefix = IsProton ? new DirectoryInfo(Path.Combine(protonInfo.Prefix.FullName, "pfx")) : prefix;
        ProtonPrefix = IsProton ? protonInfo.Prefix.FullName : "";

        SteamPath = IsProton ? protonInfo.SteamPath : "";
        WinePath = IsProton ? Path.Combine(BinPath, "proton") : File.Exists(Path.Combine(BinPath, "wine64")) ? Path.Combine(BinPath, "wine64") : Path.Combine(BinPath, "wine");
        AltWinePath = WinePath;
        RuntimePath = IsProton ? protonInfo.RuntimePath : "";
        Arguments = "";
        if (!string.IsNullOrEmpty(RuntimePath))
        {
            var binary = Path.Combine(RuntimePath, "_v2-entry-point");
            Arguments = $"--verb=waitforexitandrun -- \"{Path.Combine(BinPath, "proton")}\"";
            WinePath = binary;
        }
        var sb = new System.Text.StringBuilder();
        if (protonInfo is not null)
            foreach(var mount in protonInfo.SteamCompatMounts)
                sb.Append(mount + ":");
        SteamCompatMounts = sb.ToString();
    }
}