using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility;

public class WineSettings
{
    public bool IsManaged { get; private set; }

    public bool IsULWGL { get; private set; }

    public string WineServerPath { get; private set; }

    public string WinePath { get; private set; }

    public string ProtonPath { get; private set; }

    private string BinPath;

    public string FolderName { get; private set; }

    public string DownloadUrl { get; private set; }

    public string EsyncOn { get; private set; }

    public string FsyncOn { get; private set; }

    public string DebugVars { get; private set; }

    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    public WineSettings(bool isManaged, bool isULWGL, string customBinPath, string managedFolder, string managedUrl, DirectoryInfo storageFolder, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
    {
        // storageFolder is the path to .xlcore folder. managedFolder is the foldername inside the tarball that will be downloaded from managedUrl.
        IsManaged = isManaged;
        IsULWGL = isULWGL;
        FolderName = managedFolder;
        DownloadUrl = managedUrl;

        if (IsULWGL)
        {
            BinPath = Path.Combine(System.Environment.GetEnvironmentVariable("HOME"), ".local", "share", "ULWGL");
            WineServerPath = Path.Combine(customBinPath, "files", "bin", "wineserver");
            WinePath = Path.Combine(BinPath, "ulwgl-run");
            ProtonPath = customBinPath;
        }
        else
        {
            BinPath = (isManaged) ? Path.Combine(storageFolder.FullName, "compatibilitytool", "wine", managedFolder, "bin") : customBinPath;
            WineServerPath = Path.Combine(BinPath, "wineserver");
            WinePath = File.Exists(Path.Combine(BinPath, "wine64")) ? Path.Combine(BinPath, "wine64") : Path.Combine(BinPath, "wine");
            ProtonPath = "";
        }
        this.EsyncOn = (esyncOn ?? false) ? "1" : "0";
        this.FsyncOn = (fsyncOn ?? false) ? "1" : "0";
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
    }
}