using System;
using System.IO;
using System.Runtime.InteropServices;

namespace XIVLauncher.Common;

public class Storage
{
    public DirectoryInfo Root { get; }

    public Storage(string appName, string? overridePath = null)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            this.Root = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)  || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Use XDG_DATA_HOME on Linux.
            this.Root = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName));
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