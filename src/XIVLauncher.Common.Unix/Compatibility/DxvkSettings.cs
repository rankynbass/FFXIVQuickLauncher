using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public class DxvkSettings
{
    public bool Enabled { get; }

    public string FolderName { get; }

    public string DownloadUrl { get; }

    public string NvapiFolderName { get; }

    public string NvapiDownloadUrl { get; }

    public string NvngxFolder { get; }

    public bool NvapiEnabled { get; }

    public Dictionary<string, string> Environment { get; }

    public DxvkSettings(string folder, string url, string storageFolder, bool async, int maxFrameRate, bool dxvkHudEnabled, string dxvkHudString, bool mangoHudEnabled = false, bool mangoHudCustomIsFile = false, string customMangoHud = "", bool enabled = true, string nvapiFolder = "", string nvapiUrl = "", string nvngxFolder = "")
    {
        FolderName = folder;
        DownloadUrl = url;
        NvapiFolderName = nvapiFolder;
        NvapiDownloadUrl = nvapiUrl;
        NvngxFolder = nvngxFolder;
        Enabled = enabled;

        // Disable Nvapi if the NvapiFolderName is empty, if Dxvk is not enabled, or if the dxvk version is dxvk-1.x or dxvk-async-1.x
        NvapiEnabled = (!string.IsNullOrEmpty(NvapiFolderName) && DxvkAllowsNvapi(FolderName) && Enabled);
        System.Console.WriteLine($"NvapiEnabled = {NvapiEnabled}");


        var dxvkConfigPath = new DirectoryInfo(Path.Combine(storageFolder, "compatibilitytool", "dxvk"));
        Environment = new Dictionary<string, string>
        {
            { "DXVK_LOG_PATH", Path.Combine(storageFolder, "logs") },
            { "DXVK_CONFIG_FILE", Path.Combine(dxvkConfigPath.FullName, "dxvk.conf") },
        };
        
        if (maxFrameRate != 0)
            Environment.Add("DXVK_FRAME_RATE", (maxFrameRate).ToString());
        
        if (async)
            Environment.Add("DXVK_ASYNC", "1");
        
        var dxvkCachePath = new DirectoryInfo(Path.Combine(dxvkConfigPath.FullName, "cache"));
        if (!dxvkCachePath.Exists) dxvkCachePath.Create();
        Environment.Add("DXVK_STATE_CACHE_PATH", Path.Combine(dxvkCachePath.FullName, folder));

        if (dxvkHudEnabled)
            Environment.Add("DXVK_HUD", DxvkHudStringIsValid(dxvkHudString) ? dxvkHudString : "1");

        if (mangoHudEnabled && MangoHudIsInstalled())
        {
            Environment.Add("MANGOHUD", "1");
            if (mangoHudCustomIsFile)
            {
                if (File.Exists(customMangoHud))
                    Environment.Add("MANGOHUD_CONFIGFILE", customMangoHud);
                else
                    Environment.Add("MANGOHUD_CONFIG", "");
            }
            else
            {
                Environment.Add("MANGOHUD_CONFIG", customMangoHud);
            }
        }

        if (NvapiEnabled)
        {
            Environment.Add("DXVK_ENABLE_NVAPI", "1");
        }
    }

    public static bool DxvkHudStringIsValid(string customHud)
    {
        var ALLOWED_CHARS = "^[0-9a-zA-Z,=.]+$";
        var ALLOWED_WORDS = "^(?:devinfo|fps|frametimes|submissions|drawcalls|pipelines|descriptors|memory|gpuload|version|api|cs|compiler|samplers|scale=(?:[0-9])*(?:.(?:[0-9])+)?)$";

        if (string.IsNullOrWhiteSpace(customHud)) return false;
        if (customHud == "full") return true;
        if (customHud == "1") return true;
        if (!Regex.IsMatch(customHud, ALLOWED_CHARS)) return false;

        string[] hudvars = customHud.Split(",");

        return hudvars.All(hudvar => Regex.IsMatch(hudvar, ALLOWED_WORDS));        
    }

    public static bool DxvkAllowsNvapi(string dxvkVersion)
    {
        var pattern = @"^dxvk-(async-)?1\.\d{1,2}(\.\d)?$";
        return !Regex.IsMatch(dxvkVersion, pattern);
    }

    public static bool MangoHudIsInstalled()
    {
        var usrLib = Path.Combine("/", "usr", "lib", "mangohud", "libMangoHud.so"); // fedora uses this
        var usrLib64 = Path.Combine("/", "usr", "lib64", "mangohud", "libMangoHud.so"); // arch and openSUSE use this
        var flatpak = Path.Combine("/", "usr", "lib", "extensions", "vulkan", "MangoHud", "lib", "x86_64-linux-gnu", "libMangoHud.so");
        var debuntu = Path.Combine("/", "usr", "lib", "x86_64-linux-gnu", "mangohud", "libMangoHud.so");
        if (File.Exists(usrLib64) || File.Exists(usrLib) || File.Exists(flatpak) || File.Exists(debuntu))
            return true;
        return false;
    }
}