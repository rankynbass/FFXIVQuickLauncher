using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using XIVLauncher.Common.Util;
using Serilog;

#if FLATPAK
#warning THIS IS A FLATPAK BUILD!!!
#endif

namespace XIVLauncher.Common.Unix.Compatibility;

public class CompatibilityTools
{
    private DirectoryInfo wineDirectory;
    
    private DirectoryInfo dxvkDirectory;

    private DirectoryInfo compatToolsDirectory;

    private StreamWriter logWriter;

    public bool IsToolReady { get; private set; }

    public WineSettings Settings { get; private set; }

    public DxvkSettings DxvkSettings { get; private set; }

    private DirectoryInfo GamePath;

    private DirectoryInfo GameConfigPath;

    public bool IsToolDownloaded => File.Exists(Settings.WinePath) && Settings.Prefix.Exists;

    public bool IsFlatpak { get; }
    
    private readonly bool gamemodeOn;

    private const string GAMEID = "39210";

    private Dictionary<string, string> extraEnvironmentVars;

    public CompatibilityTools(WineSettings wineSettings, DxvkSettings dxvkSettings, bool? gamemodeOn, DirectoryInfo toolsFolder, DirectoryInfo steamFolder, DirectoryInfo gamePath, DirectoryInfo gameConfigPath, bool isFlatpak, Dictionary<string, string> extraEnvVars = null)
    {
        this.Settings = wineSettings;
        this.DxvkSettings = dxvkSettings;
        this.gamemodeOn = gamemodeOn ?? false;
        this.GamePath = gamePath;
        this.GameConfigPath = gameConfigPath;

        // These are currently unused. Here for future use. 
        this.IsFlatpak = isFlatpak;
        this.extraEnvironmentVars = extraEnvVars ?? new Dictionary<string, string>();

        this.wineDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "wine"));
        this.dxvkDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "dxvk"));
        this.compatToolsDirectory = new DirectoryInfo(Path.Combine(steamFolder.FullName, "compatibilitytools.d"));

        this.logWriter = new StreamWriter(wineSettings.LogFile.FullName);

        if (!this.wineDirectory.Exists)
            this.wineDirectory.Create();

        if (!this.dxvkDirectory.Exists)
            this.dxvkDirectory.Create();

        if (!this.compatToolsDirectory.Exists && wineSettings.IsProton)
            this.compatToolsDirectory.Create();

        if (!wineSettings.Prefix.Exists)
        {
            wineSettings.Prefix.Create();
            if (wineSettings.IsProton)
                File.CreateSymbolicLink(Path.Combine(wineSettings.Prefix.FullName, "pfx"), wineSettings.Prefix.FullName);
        }
    }

    public async Task EnsureTool(DirectoryInfo tempPath)
    {
        // if (Settings.IsProton)
        // {
        //     if (!File.Exists(Settings.WinePath))
        //         throw new FileNotFoundException("Selected proton version not found");
        //     IsToolReady = true;
        //     EnsurePrefix();
        //     return;
        // }

        if (!File.Exists(Settings.WinePath))
        {
            if (!Settings.IsManaged)
                throw new FileNotFoundException($"{(Settings.IsProton ? "Proton version" : "Wine version")} not found at the given path: {Settings.WinePath}");
            Log.Information($"Compatibility tool does not exist, downloading {Settings.DownloadUrl}");
            await DownloadTool(Settings.IsProton ? compatToolsDirectory : wineDirectory, Settings.DownloadUrl).ConfigureAwait(false);
        }
        EnsurePrefix();
        
        if (DxvkSettings.Enabled)
            await InstallDxvk().ConfigureAwait(false);

        IsToolReady = true;
    }

    private async Task InstallDxvk()
    {
        var dxvkPath = Path.Combine(dxvkDirectory.FullName, DxvkSettings.FolderName, "x64");
        if (!Directory.Exists(dxvkPath))
        {
            Log.Information($"DXVK does not exist, downloading {DxvkSettings.DownloadUrl}");
            await DownloadTool(dxvkDirectory, DxvkSettings.DownloadUrl).ConfigureAwait(false);
        }

        var system32 = Path.Combine(Settings.Prefix.FullName, "drive_c", "windows", "system32");
        var files = Directory.GetFiles(dxvkPath);

        foreach (string fileName in files)
        {
            File.Copy(fileName, Path.Combine(system32, Path.GetFileName(fileName)), true);
        }

        // 32-bit files. Probably not needed anymore, but may be useful for running other programs in prefix.
        var dxvkPath32 = Path.Combine(dxvkDirectory.FullName, DxvkSettings.FolderName, "x32");
        var syswow64 = Path.Combine(Settings.Prefix.FullName, "drive_c", "windows", "syswow64");

        if (Directory.Exists(dxvkPath32))
        {
            files = Directory.GetFiles(dxvkPath32);

            foreach (string fileName in files)
            {
                File.Copy(fileName, Path.Combine(syswow64, Path.GetFileName(fileName)), true);
            }
        }   
    }

    private async Task DownloadTool(DirectoryInfo installDirectory, string downloadUrl)
    {
        using var client = new HttpClient();
        var tempPath = Path.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(downloadUrl));
        PlatformHelpers.Untar(tempPath, installDirectory.FullName);

        File.Delete(tempPath);
    }

    private void ResetPrefix()
    {
        Settings.Prefix.Refresh();

        if (Settings.Prefix.Exists)
            Settings.Prefix.Delete(true);

        Settings.Prefix.Create();
        if (Settings.IsProton)
            File.CreateSymbolicLink(Path.Combine(Settings.Prefix.FullName, "pfx"), Settings.Prefix.FullName);

        EnsurePrefix();
    }

    public void EnsurePrefix()
    {
        RunWithoutContainer("cmd /c dir %userprofile%/Documents > nul", false).WaitForExit();
    }

    // This function exists to speed up launch times when using umu-run. umu-run isn't needed
    // to do a few basic things like checking paths and initializing the prefix; no need to load the container.
    public Process RunWithoutContainer(string command, bool runinprefix = true)
    {
        var psi = new ProcessStartInfo(Settings.WinePath);
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.Environment.Add("WINEPREFIX", Settings.Prefix.FullName);
        if (Settings.IsProton)
        {
            psi.Environment.Add("STEAM_COMPAT_DATA_PATH", Settings.Prefix.FullName);
            psi.Environment.Add("STEAM_COMPAT_INSTALL_PATH", "");
            if (!Settings.FsyncOn)
            {
                psi.Environment.Add("PROTON_NO_FSYNC", "1");
                if (!Settings.EsyncOn)
                    psi.Environment.Add("PROTON_NO_ESYNC", "1");
            }
            if (!DxvkSettings.Enabled)
                psi.Environment.Add("PROTON_USE_WINED3D", "1");
        }
        else
        {
            psi.Environment.Add("WINEESYNC", Settings.EsyncOn ? "1" : "0");
            psi.Environment.Add("WINEFSYNC", Settings.FsyncOn ? "1" : "0");
        }
        psi.Environment.Add("WINEDLLOVERRIDES", $"msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi={(DxvkSettings.Enabled ? "n,b" : "b")}");
        psi.Arguments = runinprefix ? Settings.RunInPrefix + command : Settings.Run + command;
        var quickRun = new Process();
        quickRun.StartInfo = psi;
        quickRun.Start();
        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, psi.Arguments);
        return quickRun;
    }

    public Process RunInPrefix(string command, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Settings.Runner);
        psi.Arguments = (Settings.IsProton && !Settings.IsUmu) ? Settings.RunInPrefix + command : command;

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, psi.Arguments);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Settings.Runner);
        if (Settings.IsProton && !Settings.IsUmu)
            psi.ArgumentList.Add(Settings.RunInPrefix.Trim());
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, psi.ArgumentList.Aggregate(string.Empty, (a, b) => a + " " + b));
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    private void MergeDictionaries(IDictionary<string, string> a, IDictionary<string, string> b)
    {
        if (b is null)
            return;

        foreach (var keyValuePair in b)
        {
            if (a.ContainsKey(keyValuePair.Key))
            {
                if (keyValuePair.Key == "LD_PRELOAD")
                    a[keyValuePair.Key] = MergeLDPreload(a[keyValuePair.Key], keyValuePair.Value);
                else
                    a[keyValuePair.Key] = keyValuePair.Value;
            }
            else
                a.Add(keyValuePair.Key, keyValuePair.Value);
        }
    }

    private string MergeLDPreload(string a, string b)
    {
        a ??= "";
        b ??= "";
        return (a.Trim(':') + ":" + b.Trim(':')).Trim(':');
    }

    public void AddEnvironmentVar(string key, string value)
    {
        extraEnvironmentVars.Add(key, value);
    }

    public void AddEnvironmentVars(IDictionary<string, string> env)
    {
        MergeDictionaries(extraEnvironmentVars, env);
    }

    private Process RunInPrefix(ProcessStartInfo psi, string workingDirectory, IDictionary<string, string> environment, bool redirectOutput, bool writeLog, bool wineD3D)
    {
        psi.RedirectStandardOutput = redirectOutput;
        psi.RedirectStandardError = writeLog;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workingDirectory;

        wineD3D = !DxvkSettings.Enabled || wineD3D;

        var wineEnvironmentVariables = new Dictionary<string, string>();
        wineEnvironmentVariables.Add("WINEPREFIX", Settings.Prefix.FullName);
        if (Settings.IsUmu)
        {
            // Need to use runinprefix, or it will (depending on proton version) launch Dalamud in a separate wine cmd prompt. This prevents the process from being found.
            if (!environment.ContainsKey("PROTON_VERB"))
                environment.Add("PROTON_VERB", "runinprefix");
            wineEnvironmentVariables.Add("GAMEID", "XIVLauncher.Core");
            wineEnvironmentVariables.Add("PROTONPATH", new FileInfo(Settings.WinePath).DirectoryName);
            var importantPaths = new System.Text.StringBuilder(GamePath.FullName + ":" + GameConfigPath.FullName);
            var steamCompatMounts = System.Environment.GetEnvironmentVariable("STEAM_COMPAT_MOUNTS");
            var pressureVesselFilesystems = System.Environment.GetEnvironmentVariable("PRESSURE_VESSEL_FILESYSTEMS_RW");
            var xlCompatMounts = System.Environment.GetEnvironmentVariable("XL_COMPAT_MOUNTS");
            if (!string.IsNullOrEmpty(steamCompatMounts))
                importantPaths.Append(":" + steamCompatMounts);
            if (!string.IsNullOrEmpty(pressureVesselFilesystems))
                importantPaths.Append(":" + pressureVesselFilesystems);
            if (!string.IsNullOrEmpty(xlCompatMounts))
                importantPaths.Append(":" + xlCompatMounts);
            var runtimeDir = System.Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (string.IsNullOrEmpty(runtimeDir))
            {
                var id = new ProcessStartInfo("id");
                id.RedirectStandardOutput = true;
                id.UseShellExecute = false;
                id.Arguments = "-u";
                var getUID = new Process();
                getUID.StartInfo = id;
                getUID.Start();
                runtimeDir = "/run/user/" + getUID.StandardOutput.ReadToEnd().Trim();
            }
            for (int i = 0; i < 10; i++)
                importantPaths.Append($":{runtimeDir}/discord-ipc-{i}");
            importantPaths.Append($"{runtimeDir}/app/com.discordapp.Discord:{runtimeDir}/snap.discord-canary");
            
            wineEnvironmentVariables.Add("PRESSURE_VESSEL_FILESYSTEMS_RW", importantPaths.ToString());
        }
        else if (Settings.IsProton)
        {
            wineEnvironmentVariables.Add("STEAM_COMPAT_DATA_PATH", Settings.Prefix.FullName);
            wineEnvironmentVariables.Add("STEAM_COMPAT_INSTALL_PATH", "");
        }

        wineEnvironmentVariables.Add("WINEDLLOVERRIDES", $"msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi={(wineD3D ? "b" : "n,b")}");

        if (!string.IsNullOrEmpty(Settings.DebugVars))
        {
            wineEnvironmentVariables.Add("WINEDEBUG", Settings.DebugVars);
        }

        wineEnvironmentVariables.Add("XL_WINEONLINUX", "true");

        if (this.gamemodeOn)
            wineEnvironmentVariables.Add("LD_PRELOAD", MergeLDPreload("libgamemodeauto.so.0" , Environment.GetEnvironmentVariable("LD_PRELOAD")));

        if (Settings.IsProton)
        {
            if (!Settings.FsyncOn)
            {
                wineEnvironmentVariables.Add("PROTON_NO_FSYNC", "1");
                if (!Settings.EsyncOn)
                    wineEnvironmentVariables.Add("PROTON_NO_ESYNC", "1");
            }
            if (wineD3D)
                wineEnvironmentVariables.Add("PROTON_USE_WINED3D", "1");

        }
        else
        {
            wineEnvironmentVariables.Add("WINEESYNC", Settings.EsyncOn ? "1" : "0");
            wineEnvironmentVariables.Add("WINEFSYNC", Settings.FsyncOn ? "1" : "0");
        }

        foreach (var dxvkVar in DxvkSettings.Environment)
            wineEnvironmentVariables.Add(dxvkVar.Key, dxvkVar.Value);

        MergeDictionaries(psi.Environment, wineEnvironmentVariables);
        MergeDictionaries(psi.Environment, extraEnvironmentVars);       // Allow extraEnvironmentVars to override what we set here.
        MergeDictionaries(psi.Environment, environment);

#if FLATPAK_NOTRIGHTNOW
        psi.FileName = "flatpak-spawn";

        psi.ArgumentList.Insert(0, "--host");
        psi.ArgumentList.Insert(1, Settings.WinePath);

        foreach (KeyValuePair<string, string> envVar in wineEnvironmentVariables)
        {
            psi.ArgumentList.Insert(1, $"--env={envVar.Key}={envVar.Value}");
        }

        if (environment != null)
        {
            foreach (KeyValuePair<string, string> envVar in environment)
            {
                psi.ArgumentList.Insert(1, $"--env=\"{envVar.Key}\"=\"{envVar.Value}\"");
            }
        }
#endif

        Process helperProcess = new();
        helperProcess.StartInfo = psi;
        helperProcess.ErrorDataReceived += new DataReceivedEventHandler((_, errLine) =>
        {
            if (String.IsNullOrEmpty(errLine.Data))
                return;

            try
            {
                logWriter.WriteLine(errLine.Data);
                Console.Error.WriteLine(errLine.Data);
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException ||
                                       ex is OverflowException ||
                                       ex is IndexOutOfRangeException)
            {
                // very long wine log lines get chopped off after a (seemingly) arbitrary limit resulting in strings that are not null terminated
                //logWriter.WriteLine("Error writing Wine log line:");
                //logWriter.WriteLine(ex.Message);
            }
        });

        helperProcess.Start();
        if (writeLog)
            helperProcess.BeginErrorReadLine();

        return helperProcess;
    }

    public Int32[] GetProcessIds(string executableName)
    {
        var wineDbg = RunInPrefix("winedbg --command \"info proc\"", redirectOutput: true);
        var output = wineDbg.StandardOutput.ReadToEnd();
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(l => l.Contains(executableName));
        return matchingLines.Select(l => int.Parse(l.Substring(1, 8), System.Globalization.NumberStyles.HexNumber)).ToArray();
    }

    public Int32 GetProcessId(string executableName)
    {
        return GetProcessIds(executableName).FirstOrDefault();
    }

    public Int32 GetUnixProcessId(Int32 winePid)
    {
        var environment = new Dictionary<string, string>();
        var wineDbg = RunInPrefix("winedbg --command \"info procmap\"", redirectOutput: true, environment: environment);
        var output = wineDbg.StandardOutput.ReadToEnd();
        if (output.Contains("syntax error\n") || output.Contains("Exception c0000005")) // Proton8 wine changed the error message
        {
            var processName = GetProcessName(winePid);
            return GetUnixProcessIdByName(processName);
        }
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Where(
            l => int.Parse(l.Substring(1, 8), System.Globalization.NumberStyles.HexNumber) == winePid);
        var unixPids = matchingLines.Select(l => int.Parse(l.Substring(10, 8), System.Globalization.NumberStyles.HexNumber)).ToArray();
        return unixPids.FirstOrDefault();
    }

    private string GetProcessName(Int32 winePid)
    {
        var environment = new Dictionary<string, string>();
        var wineDbg = RunInPrefix("winedbg --command \"info proc\"", redirectOutput: true, environment: environment);
        var output = wineDbg.StandardOutput.ReadToEnd();
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Where(
            l => int.Parse(l.Substring(1, 8), System.Globalization.NumberStyles.HexNumber) == winePid);
        var processNames = matchingLines.Select(l => l.Substring(20).Trim('\'')).ToArray();
        return processNames.FirstOrDefault();
    }

    private Int32 GetUnixProcessIdByName(string executableName)
    {
        int closest = 0;
        int early = 0;
        var currentProcess = Process.GetCurrentProcess(); // Gets XIVLauncher.Core's process
        bool nonunique = false;
        foreach (var process in Process.GetProcessesByName(executableName))
        {
            if (process.Id < currentProcess.Id)
            {
                early = process.Id;
                continue;  // Process was launched before XIVLauncher.Core
            }
            // Assume that the closest PID to XIVLauncher.Core's is the correct one. But log an error if more than one is found.
            if ((closest - currentProcess.Id) > (process.Id - currentProcess.Id) || closest == 0)
            {
                if (closest != 0) nonunique = true;
                closest = process.Id;
            }
            if (nonunique) Log.Error($"More than one {executableName} found! Selecting the most likely match with process id {closest}.");
        }
        // Deal with rare edge-case where pid rollover causes the ffxiv pid to be lower than XLCore's.
        if (closest == 0 && early != 0) closest = early;
        if (closest != 0) Log.Verbose($"Process for {executableName} found using fallback method: {closest}. XLCore pid: {currentProcess.Id}");
        return closest;
    }

    public string UnixToWinePath(string unixPath)
    {
        var launchArguments = $"winepath --windows \"{unixPath}\"";
        var winePath = RunWithoutContainer(launchArguments);
        var output = winePath.StandardOutput.ReadToEnd();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    public void AddRegistryKey(string key, string value, string data)
    {
        var args = $"reg add {key} /v {value} /d {data} /f";
        var wineProcess = RunWithoutContainer(args);
        wineProcess.WaitForExit();
    }

    public void Kill()
    {
        var psi = new ProcessStartInfo(Settings.WineServerPath)
        {
            Arguments = "-k"
        };
        psi.EnvironmentVariables.Add("WINEPREFIX", Settings.Prefix.FullName);

        Process.Start(psi);
    }
}