﻿using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility;

public static class Dxvk
{
    public static async Task InstallDxvk(DirectoryInfo prefix, DirectoryInfo installDirectory, DxvkSettings dxvkSettings)
    {
        if (!dxvkSettings.Enabled) return;
        var dxvkPath = Path.Combine(installDirectory.FullName, dxvkSettings.FolderName, "x64");

        if (!Directory.Exists(dxvkPath))
        {
            Log.Information("DXVK does not exist, downloading");
            await DownloadDxvk(installDirectory, dxvkSettings.DownloadURL).ConfigureAwait(false);
        }

        var system32 = Path.Combine(prefix.FullName, "drive_c", "windows", "system32");
        var files = Directory.GetFiles(dxvkPath);

        foreach (string fileName in files)
        {
            File.Copy(fileName, Path.Combine(system32, Path.GetFileName(fileName)), true);
        }
    }

    private static async Task DownloadDxvk(DirectoryInfo installDirectory, string downloadURL)
    {
        using var client = new HttpClient();
        var tempPath = Path.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(downloadURL));
        PlatformHelpers.Untar(tempPath, installDirectory.FullName);

        File.Delete(tempPath);
    }

    public enum DxvkHudType
    {
        [SettingsDescription("None", "Show nothing")]
        None,

        [SettingsDescription("FPS", "Only show FPS")]
        Fps,

        [SettingsDescription("DXVK Hud Custom", "Use a custom DXVK_HUD string")]
        Custom,

        [SettingsDescription("Full", "Show everything")]
        Full,

        [SettingsDescription("MangoHud Default", "Uses no config file.")]
        MangoHud,

        [SettingsDescription("MangoHud Custom", "Specify a custom config file")]
        MangoHudCustom,

        [SettingsDescription("MangoHud Full", "Show (almost) everything")]
        MangoHudFull,
    }

    public enum DxvkVersion
    {
        [SettingsDescription("1.10.3 (default)", "Current version of 1.10 branch of DXVK.")]
        v1_10_3,

        [SettingsDescription("2.0", "Newer version of DXVK. Last version with Async patch")]
        v2_0,

        [SettingsDescription("2.1 (No Async)", "Newer version of DXVK, using graphics pipeline library. No Async patch.")]
        v2_1,

         [SettingsDescription("2.2 (No Async)", "Newest version of DXVK, using graphics pipeline library. No Async patch.")]
        v2_2,     

        [SettingsDescription("Disabled (OpenGL)", "Disable Dxvk, use WineD3D / OpenGL instead.")]
        Disabled,

        [SettingsDescription("Disabled (Vulkan)", "Disable Dxvk, use WineD3D / Vulkan instead (experimental).")]
        DisabledVK,

    }
}
