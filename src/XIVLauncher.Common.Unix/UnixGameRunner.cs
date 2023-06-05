using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix;

public class UnixGameRunner : IGameRunner
{
    private readonly CompatibilityTools compatibility;
    private readonly DalamudLauncher dalamudLauncher;
    private readonly bool dalamudOk;

    public UnixGameRunner(CompatibilityTools compatibility, DalamudLauncher dalamudLauncher, bool dalamudOk)
    {
        this.compatibility = compatibility;
        this.dalamudLauncher = dalamudLauncher;
        this.dalamudOk = dalamudOk;
    }

    public Process? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness)
    {
        if (!compatibility.DxvkSettings.Enabled)
        {
            string regedit = $"reg add HKEY_CURRENT_USER\\Software\\Wine\\Direct3D /v renderer /t REG_SZ /d {compatibility.DxvkSettings.WineD3DBackend} /f";
            System.Console.WriteLine("Trying to set registry key: 'wine " + regedit + "'");
            compatibility.RunInPrefix(regedit);
        }
        if (dalamudOk)
            return this.dalamudLauncher.Run(new FileInfo(path), arguments, environment);

        return compatibility.RunInPrefix($"\"{path}\" {arguments}", workingDirectory, environment, writeLog: true, inject: false);
    }
}