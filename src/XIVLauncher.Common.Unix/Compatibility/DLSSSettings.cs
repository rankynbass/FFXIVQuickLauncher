using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public class DLSSSettings
{
    public bool Enabled { get; private set; }

    public bool NoOverwrite { get; private set; }

    public string FolderName { get; }

    public string DownloadUrl { get; }

    public string NvidiaWineFolder { get; }

    public List<string> NvidiaFiles { get; }

    public Dictionary<string, string> Environment { get; }

    // Constructor for Wine
    public DLSSSettings(bool enabled, bool noOverwrite, string folder, string url, string nvidiaFolder, List<string> nvidiaFiles = null)
    {
        Enabled = enabled;
        NoOverwrite = noOverwrite;
        FolderName = folder;
        DownloadUrl = url;
        NvidiaWineFolder = nvidiaFolder;
        NvidiaFiles = nvidiaFiles ?? new List<string> { "nvngx.dll", "_nvngx.dll" };
        Environment = new Dictionary<string, string>();
        if (Enabled)
            Environment.Add("DXVK_ENABLE_NVAPI", "1");
    }

    // Constructor for Proton
    public DLSSSettings(bool enabled, List<string> nvidiaFiles = null)
    {
        Enabled = enabled;
        NoOverwrite = false;
        FolderName = "";
        DownloadUrl = "";
        NvidiaFiles = nvidiaFiles ?? new List<string>();
        NvidiaWineFolder = "";
        Environment = new Dictionary<string, string>();
        if (!Enabled)
            Environment.Add("PROTON_DISABLE_NVAPI", "1");
    }

    internal async Task Install(DirectoryInfo dxvkDirectory, DirectoryInfo prefix)
    {
        if (string.IsNullOrEmpty(FolderName))
        {
            Log.Error($"Invalid Nvapi Folder (folder name is empty)");
            return;
        }

        var dxvkPath = Path.Combine(dxvkDirectory.FullName, FolderName, "x64");
        if (!Directory.Exists(dxvkPath))
        {
            Log.Information($"DXVK does not exist, downloading {DownloadUrl}");
            await CompatibilityTools.DownloadTool(dxvkDirectory, DownloadUrl).ConfigureAwait(false);
        }

        var system32 = Path.Combine(prefix.FullName, "drive_c", "windows", "system32");
        var files = Directory.GetFiles(dxvkPath);

        foreach (string fileName in files)
        {
            File.Copy(fileName, Path.Combine(system32, Path.GetFileName(fileName)), true);
        }

        // 32-bit files. Probably not needed anymore, but may be useful for running other programs in prefix.
        var dxvkPath32 = Path.Combine(dxvkDirectory.FullName, FolderName, "x32");
        var syswow64 = Path.Combine(prefix.FullName, "drive_c", "windows", "syswow64");

        if (Directory.Exists(dxvkPath32))
        {
            files = Directory.GetFiles(dxvkPath32);

            foreach (string fileName in files)
            {
                File.Copy(fileName, Path.Combine(syswow64, Path.GetFileName(fileName)), true);
            }
        }
    }

    internal void InstallNvidaFiles(DirectoryInfo gamePath)
    {
        // Create symlinks to nvngx.dll and _nvngx.dll in the GamePath/game folder. For some reason it doesn't work if you put them in system32.
        // If NvngxOverride is set, assume the files/symlinks are already there. For Nix compatibility, mostly.
        if (string.IsNullOrEmpty(NvidiaWineFolder))
        {
            return;
        }
        if (!Directory.Exists(NvidiaWineFolder))
        {
            return;
        }

        if (NvidiaFiles.Count == 0)
        {
            return;
        }

        foreach (var target in NvidiaFiles)
        {
            var source = new FileInfo(Path.Combine(NvidiaWineFolder, target));
            var destination = new FileInfo(Path.Combine(gamePath.FullName, "game", target));
            if (source.Exists)
            {
                if (!destination.Exists) // No file, create link.
                {
                    destination.CreateAsSymbolicLink(source.FullName);
                    Log.Verbose($"Making symbolic link at {destination.FullName} to {source.FullName}");
                }
                else if (destination.ResolveLinkTarget(false) is null) // File exists, is not a symlink. Delete and create link.
                {
                    destination.Delete();
                    destination.CreateAsSymbolicLink(source.FullName);
                    Log.Verbose($"Replacing file at {destination.FullName} with symbolic link to {source.FullName}");
                }
                else if (destination.ResolveLinkTarget(true).FullName != source.FullName) // Link exists, but does not point to source. Replace.
                {
                    destination.Delete();
                    destination.CreateAsSymbolicLink(source.FullName);
                    Log.Verbose($"Symbolic link at {destination.FullName} incorrectly links to {destination.ResolveLinkTarget(true).FullName}. Replacing with link to {source.FullName}");
                }
                else
                    Log.Verbose($"Symbolic link at {destination.FullName} to {source.FullName} is correct.");
            }
            else
                Log.Error($"Missing Nvidia file! DLSS may not work. {target} not found in {NvidiaWineFolder}");
        }
    }
}