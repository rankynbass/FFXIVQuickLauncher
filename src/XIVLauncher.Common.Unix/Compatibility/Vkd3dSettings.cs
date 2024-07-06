using System.IO;
using System.Collections.Generic;

namespace XIVLauncher.Common.Unix.Compatibility;

public class Vkd3dSettings
{
    public bool Enabled { get; }

    public string FolderName { get; }

    public string DownloadUrl { get; }

    public Dictionary<string, string> Environment { get; }

    public Vkd3dSettings(string folder, string url, string storageFolder, bool enabled)
    {
        FolderName = folder;
        DownloadUrl = url;
        Enabled = enabled;

        var vkd3dConfigPath = new DirectoryInfo(Path.Combine(storageFolder, "compatibilitytool", "vkd3d"));
        Environment = new Dictionary<string, string>
        {
            { "VKD3D_LOG_FINE", Path.Combine(storageFolder, "logs") },
        };
    }
}