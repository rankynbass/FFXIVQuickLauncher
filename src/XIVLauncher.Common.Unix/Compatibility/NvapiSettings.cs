using System.IO;
using System.Collections.Generic;

namespace XIVLauncher.Common.Unix.Compatibility;

public class NvapiSettings
{
    public bool Enabled { get; }

    public string FolderName { get; }

    public string DownloadUrl { get; }

    public Dictionary<string, string> Environment { get; }

    public NvapiSettings(string folder, string url, string storageFolder, bool enabled)
    {
        FolderName = folder;
        DownloadUrl = url;
        Enabled = enabled;

        var nvapiConfigPath = new DirectoryInfo(Path.Combine(storageFolder, "compatibilitytool", "nvapi"));
        Environment = new Dictionary<string, string>
        {
            { "DXVK_NVAPI_LOG_PATH", Path.Combine(storageFolder, "logs") },
            { "DXVK_NVAPI_LOG_LEVEL", "info" },
            { "DXVK_ENABLE_NVAPI", Enabled ? "1" : "0" },
        };
    }
}