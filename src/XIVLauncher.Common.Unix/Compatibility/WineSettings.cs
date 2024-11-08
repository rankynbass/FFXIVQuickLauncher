using System.IO;
using System.Text.RegularExpressions;
using System.Linq;


namespace XIVLauncher.Common.Unix.Compatibility;

public class WineSettings
{
    public bool IsManaged { get; private set; }

    public string WineServerPath => Path.Combine(BinPath, "wineserver");

    public string WinePath => File.Exists(Path.Combine(BinPath, "wine64")) ? Path.Combine(BinPath, "wine64") : Path.Combine(BinPath, "wine");

    private string BinPath;

    public string FolderName;

    public string DownloadUrl;

    public string ExtraWineDLLOverrides;

    public string EsyncOn { get; private set; }

    public string FsyncOn { get; private set; }

    public string DebugVars { get; private set; }

    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    public WineSettings(bool isManaged, string customBinPath, string managedFolder, string managedUrl, string extraDLLOverrides, DirectoryInfo storageFolder, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
    {
        // storageFolder is the path to .xlcore folder. managedFolder is the foldername inside the tarball that will be downloaded from managedUrl.
        IsManaged = isManaged;
        FolderName = managedFolder;
        DownloadUrl = managedUrl;
        BinPath = (isManaged) ? Path.Combine(storageFolder.FullName, "compatibilitytool", "wine", managedFolder, "bin") : customBinPath;
        ExtraWineDLLOverrides = WineDLLOverrideIsValid(extraDLLOverrides) ? extraDLLOverrides ?? "" : "";

        this.EsyncOn = (esyncOn ?? false) ? "1" : "0";
        this.FsyncOn = (fsyncOn ?? false) ? "1" : "0";
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
    }

    public static bool WineDLLOverrideIsValid(string dlls)
    {
        string[] invalid = { "msquic", "mscoree", "d3d9", "d3d11", "d3d10core", "dxgi" };
        var format = @"^(?:(?:[a-zA-Z0-9_\-\.]+,?)+=(?:n,b|b,n|n|b|d|,|);?)+$";

        if (string.IsNullOrEmpty(dlls)) return true;
        if (invalid.Any(s => dlls.Contains(s))) return false;
        if (Regex.IsMatch(dlls, format)) return true;

        return false;
    }
}