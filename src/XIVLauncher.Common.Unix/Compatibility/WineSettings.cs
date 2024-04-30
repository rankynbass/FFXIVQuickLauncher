using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility;

public class WineSettings
{
    public bool IsManaged => !string.IsNullOrEmpty(DownloadUrl);

    public bool IsProton { get; private set; }

    public bool IsContainer => !string.IsNullOrEmpty(ContainerPath);

    public string WineServerPath { get; private set; }

    public string WinePath { get; private set; }

    private string BinPath;

    public string DownloadUrl;

    private string ContainerPath;

    public string ContainerUrl;

    public bool EsyncOn { get; private set; }

    public bool FsyncOn { get; private set; }

    public string DebugVars { get; private set; }

    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    public string Runner => string.IsNullOrEmpty(ContainerPath) ? WinePath : ContainerPath;

    public string Run => IsProton ? "run " : "";

    public string RunInPrefix => IsProton ? "runinprefix " : "";

    public string RunInContainer => IsContainer ? $"--verb=waitforexitandrun -- \"{WinePath}\" " : "";

    public string[] RunInContainerArray => IsContainer ? new string[] {"--verb=waitforexitandrun", "--", WinePath} : new string[] { };

    public WineSettings(bool isProton, string wineFolder, string downloadUrl, string containerPath, string containerUrl, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
    {
        IsProton = isProton;
        BinPath = isProton ? wineFolder : WineCheck(wineFolder);
        WinePath = isProton ? Path.Combine(BinPath, "proton") : File.Exists(Path.Combine(BinPath, "wine64")) ? Path.Combine(BinPath, "wine64") : Path.Combine(BinPath, "wine");
        WineServerPath = isProton ? Path.Combine(BinPath, "files", "bin", "wineserver") : Path.Combine(BinPath, "wineserver");
        DownloadUrl = downloadUrl;
        ContainerPath = string.IsNullOrEmpty(containerPath) ? "" : Path.Combine(containerPath, "_v2-entry-point");
        ContainerUrl = containerUrl;

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