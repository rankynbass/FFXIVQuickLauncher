using System.Collections.Generic;
using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public class DxvkSettings
{
    public string Folder { get; }

    public string DownloadUrl { get; }

    public Dictionary<string, string> Environment { get; }

    public bool IsDxvk { get; }

    public bool IsCustom { get; }

    public DxvkSettings(string folder, string url, string rootFolder, Dictionary<string, string> env = null, bool isDxvk = true)
    {
        IsCustom = (string.IsNullOrEmpty(url)) ? true : false;
        Folder = folder;
        DownloadUrl = url;
        Environment = env ?? new Dictionary<string, string>();
        IsDxvk = isDxvk;
    }
}