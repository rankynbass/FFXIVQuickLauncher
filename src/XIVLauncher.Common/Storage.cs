using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

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
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Use XDG_DATA_HOME on Linux.
            this.Root = this.getXDGStoragePath(appName);
        }
        else
        {
            // Keeping the old path on MacOS for now.
            this.Root = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{appName}"));
        }

        if (!string.IsNullOrEmpty(overridePath))
        {
            this.Root = new DirectoryInfo(overridePath);
        }

        if (!this.Root.Exists)
            this.Root.Create();
    }

    private DirectoryInfo getXDGStoragePath(string appName)
    {
        var xdgStorage = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"dev.goats.{appName}"));
        var oldStorage = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{appName}"));

        if (xdgStorage.Exists)
            return xdgStorage;  // Use the XDG path if it already exists.

        if (oldStorage.Exists)
        {
            if (unixStorageIsMovable(xdgStorage.FullName, oldStorage.FullName))
            {
                oldStorage.MoveTo(xdgStorage.FullName); // Move ~/.{appName} to XDG_DATA_HOME/dev.goats.{appName}, since it's confirmed to be on the same drive.
                return xdgStorage;
            }
            return oldStorage; // Fallback to ~/.{appName} if we can't move it to XDG_DATA_HOME for some reason.
        }

        return xdgStorage; // Use the XDG path for new installations.
    }

    private bool unixStorageIsMovable(string unixStorage, string oldStorage)
    {
        // Future implementation note: Dotnet 11 preview currently allows for the testing hardlinks, which would allow us to try creating a hardlink
        // from a test file in the old location to a test file in the new location. If it succeeded, we'd know they were on the same drive.
        
        // We need to get the real absolute paths of both directories, resolving any symlinks, to accurately determine if they are on the same drive.
        // This is for SteamOS, Bazzite, and other similar distros, which mount things under /var and then put symlinks under /.
        var unixRealPath = unixGetRealPath(unixStorage);
        var oldRealPath = unixGetRealPath(oldStorage);

        if (unixRealPath == null || oldRealPath == null)
        {
            return false; // If we can't resolve the real path of either directory, we assume they are not movable.
        }

        try
        {
            var drives = DriveInfo.GetDrives();
            int unixHitCounter = 0;
            int oldHitCounter = 0;
            
            // Unix dotnet enumerates drives for each mounted path, including various virtual filesystems like /proc and /sys.
            // To determine if two folders are on the same drive, we'll match them to mounted partitions and count the hits.
            // This is to get around edge cases where someone did something weird like mount a partition to ~/.xlcore or ~/.local/share.
            // If they match the same number of partitions, and at least one, we can be reasonably sure they're on the same partition, and safe to move.
            foreach (var drive in drives)
            {
                if (unixRealPath.StartsWith(drive.RootDirectory.FullName))
                {
                    unixHitCounter++;
                }
                if (oldRealPath.StartsWith(drive.RootDirectory.FullName))
                {
                    oldHitCounter++;
                }
            }
            if (unixHitCounter == oldHitCounter && unixHitCounter > 0)
            {
                return true;
            }
            return false; // The files are on different drives, so they are not movable.
        }
        catch
        {
            return false; // If any exception occurs, we assume the move failed and return false.
        }
    }

    private static string? unixGetRealPath(string path)
    {
        // There's no good way to resolve the real path of a file in .NET on Linux and MacOS,
        // since .NET doesn't have a built-in way to do it. Path.GetFullPath does not work.
        // The best we can do is to call the "realpath" command-line utility, which is available on both Linux and MacOS.
        var startInfo = new ProcessStartInfo
        {
            FileName = "realpath",
            Arguments = path,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using (var process = new Process { StartInfo = startInfo })
        {
            try
            {
                process.Start();
                string realPath = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    return null;
                }
                return realPath;
            }
            catch
            {
                return null; // If any exception occurs, we assume we can't resolve the path and return null.
            }
        }
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