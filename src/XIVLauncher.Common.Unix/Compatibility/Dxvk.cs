using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility;

public static class Dxvk
{
    private static string DXVK_DOWNLOAD = "https://github.com/Sporif/dxvk-async/releases/download/1.10.1/dxvk-async-1.10.1.tar.gz";
    private static string DXVK_NAME = "dxvk-async-1.10.1";
    public static DxvkVersion Version { get; set; } = DxvkVersion.v1_10_1;

    public static async Task InstallDxvk(DirectoryInfo prefix, DirectoryInfo installDirectory)
    {
        SetDxvkVersion();
        var dxvkPath = Path.Combine(installDirectory.FullName, DXVK_NAME, "x64");

        if (!Directory.Exists(dxvkPath))
        {
            Log.Information("DXVK does not exist, downloading");
            await DownloadDxvk(installDirectory).ConfigureAwait(false);
        }

        var system32 = Path.Combine(prefix.FullName, "drive_c", "windows", "system32");
        var files = Directory.GetFiles(dxvkPath);

        foreach (string fileName in files)
        {
            File.Copy(fileName, Path.Combine(system32, Path.GetFileName(fileName)), true);
        }
    }

    private static async Task DownloadDxvk(DirectoryInfo installDirectory)
    {
        using var client = new HttpClient();
        var tempPath = Path.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(DXVK_DOWNLOAD));
        PlatformHelpers.Untar(tempPath, installDirectory.FullName);

        File.Delete(tempPath);
    }

    public enum DxvkHudType
    {
        [SettingsDescription("None", "Show nothing")]
        None,

        [SettingsDescription("FPS", "Only show FPS")]
        Fps,

        [SettingsDescription("Full", "Show everything")]
        Full,
    }

    private void SetDxvkVersion()
    {
        string DXVK_VERSION = Version switch
        {
            DxvkVersion.v1_10_1 => "1.10.1",
            DxvkVersion.v1_10_2 => "1.10.2",
            DxvkVersion.v1_10_3 => "1.10.3",
            DxvkVersion.v2_0 => "2.0",
            _ => "1.10.1",
        };
        DXVK_NAME = $"dxvk-async-{DXVK_VERSION}";
        DXVK_DOWNLOAD = $"https://github.com/Sporif/dxvk-async/releases/download/{DXVK_VERSION}/{DXVK_NAME}.tar.gz";
    }
}

public enum DxvkVersion
{
    [SettingsDescription("1.10.1 (default)", "The default version of DXVK used with XIVLauncher.Core.")]
    v1_10_1,

    [SettingsDescription("1.10.2", "Newer version of 1.10 branch of DXVK. Probably works.")]
    v1_10_2,

    [SettingsDescription("1.10.3", "Newest version of 1.https://github.com/Sporif/dxvk-async/releases/download/1.10.1/dxvk-async-1.10.1.tar.gzon of DXVK. Might break Dalamud or GShade.")]
    v2_0,
}