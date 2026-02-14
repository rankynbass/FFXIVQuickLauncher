using System;
using System.Formats.Asn1;
using System.IO;

namespace XIVLauncher.Common;

public class Storage
{
    public DirectoryInfo Root { get; }

    public Storage(string appName, string? overridePath = null)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            this.Root = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName));
        }
        else if (System.OperatingSystem.IsLinux())
        {
            var HOME = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var XDG_DATA_HOME = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrEmpty(XDG_DATA_HOME) || !Directory.Exists(XDG_DATA_HOME))
            {
                XDG_DATA_HOME = Path.Combine(HOME, ".local", "share");
            }
            var dataHomeDir = new DirectoryInfo(Path.Combine(XDG_DATA_HOME, appName));
            var xlcore = new DirectoryInfo(Path.Combine(HOME, $".{appName}"));
            if (!dataHomeDir.Exists && !xlcore.Exists)
            {
                // If neither exists, create at $XDG_DATA_HOME
                this.Root = dataHomeDir;
            }
            else if (xlcore.Exists && !dataHomeDir.Exists)
            {
                // If ~/.xlcore exists and $XDG_DATA_HOME/xlcore doesn't, move ~/.xlcore and create symlink
                xlcore.MoveTo(dataHomeDir.FullName);
                Directory.CreateSymbolicLink(xlcore.FullName, dataHomeDir.FullName);
                this.Root = dataHomeDir;
            }
            else
            {
                // Otherwise, use $XDG_DATA_HOME/xlcore
                this.Root = dataHomeDir;
            }
        }
        else
        {
            this.Root = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{appName}"));
        }

        if (!string.IsNullOrEmpty(overridePath))
        {
            this.Root = new DirectoryInfo(overridePath);
        }

        if (!this.Root.Exists)
            this.Root.Create();
    }

    public FileInfo GetFile(string fileName)
    {
        return new FileInfo(Path.Combine(this.Root.FullName, fileName));
    }

    /// <summary>
    /// Gets a folder and makes sure that it exists.
    /// </summary>
    /// <param name="folderName"></param>
    /// <returns></returns>
    public DirectoryInfo GetFolder(string folderName)
    {
        var folder = new DirectoryInfo(Path.Combine(this.Root.FullName, folderName));

        if (!folder.Exists)
            folder.Create();

        return folder;
    }
}