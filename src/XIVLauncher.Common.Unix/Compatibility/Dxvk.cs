using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility;

public static class Dxvk
{
    private const string DXVK_DOWNLOAD = "https://github.com/Sporif/dxvk-async/releases/download/1.10.1/dxvk-async-1.10.1.tar.gz";
    private const string DXVK_NAME = "dxvk-async-1.10.1";
    public static DxvkVersion Version { get; set; } = DxvkVersion.v1_10_1;
    
    public static async Task InstallDxvk(DirectoryInfo prefix, DirectoryInfo installDirectory)
    {
        var dxvkPath = Path.Combine(installDirectory.FullName, Version.Folder ?? DXVK_NAME, "x64");

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

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(Version.Download ?? DXVK_DOWNLOAD));
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
}

public class DxvkVersion
{
    // This must appear above all the other static instances
    public static List<DxvkVersion> AllVersions { get; } = new List<DxvkVersion>();

    // "Enum" entries go here.
    [SettingsDescription("1.10.1 (default)", "The default version of DXVK used with XIVLauncher.Core.")]
    public static DxvkVersion v1_10_1 {get;} = new DxvkVersion(0, "1.10.1");

    [SettingsDescription("1.10.2", "Newer version of 1.10 branch of DXVK. Probably works.")]
    public static DxvkVersion v1_10_2 {get;} = new DxvkVersion(1, "1.10.2");

    [SettingsDescription("1.10.3", "Newest version of 1.10 branch of DXVK. Probably works.")]
    public static DxvkVersion v1_10_3 {get;} = new DxvkVersion(2, "1.10.3");

    [SettingsDescription("2.0 (unsafe)", "New 2.0 version of DXVK. Might break Dalamud or GShade.")]
    public static DxvkVersion v2_0 {get;} = new DxvkVersion(3, "2.0");

    // The rest of the class to make it act like an Enum
    public int Value {get; private set; }
    public string Name { get; private set; }
    public string Folder { get; private set; }
    public string Download {get; private set; }

    private DxvkVersion(int val, string name, string? folder = null, string? download = null)
    {
        Value = val;
        Name = name;
        Folder ??= $"dxvk-async-{name}";
        Download ??= $"https://github.com/Sporif/dxvk-async/releases/download/{name}/dxvk-async-{name}.tar.gz";
    }
}