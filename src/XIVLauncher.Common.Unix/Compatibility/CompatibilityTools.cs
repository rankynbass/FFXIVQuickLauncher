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

    private DirectoryInfo commonDirectory;
    
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
        this.commonDirectory = new DirectoryInfo(Path.Combine(steamFolder.FullName, "steamapps", "common"));

        this.GamePath = gamePath;

        this.logWriter = new StreamWriter(wineSettings.LogFile.FullName);

        if (!this.wineDirectory.Exists)
            this.wineDirectory.Create();

        if (!this.dxvkDirectory.Exists)
            this.dxvkDirectory.Create();

        if (!this.compatToolsDirectory.Exists && wineSettings.IsProton)
            this.compatToolsDirectory.Create();

        if (!this.commonDirectory.Exists && wineSettings.IsRuntime)
            this.commonDirectory.Create();

        if (!wineSettings.Prefix.Exists)
        {
            wineSettings.Prefix.Create();
            if (wineSettings.IsProton)
                File.CreateSymbolicLink(Path.Combine(wineSettings.Prefix.FullName, "pfx"), wineSettings.Prefix.FullName);

        }
        else
        {
            if (File.Exists(Path.Combine(wineSettings.Prefix.FullName, "pfx")))
                File.Delete(Path.Combine(wineSettings.Prefix.FullName, "pfx"));
            if (wineSettings.IsProton && !Directory.Exists(Path.Combine(wineSettings.Prefix.FullName, "pfx")))
                File.CreateSymbolicLink(Path.Combine(wineSettings.Prefix.FullName, "pfx"), wineSettings.Prefix.FullName);
        }
    }

    public async Task EnsureTool(DirectoryInfo tempPath)
    {
        // Download the container if it's missing
        if (Settings.IsRuntime && !File.Exists(Settings.Runner))
        {
            if (string.IsNullOrEmpty(Settings.RuntimeUrl))
                throw new FileNotFoundException($"Steam runtime selected, but is not present, and no download url provided.");
            Log.Information($"Steam Linux Runtime does not exist, downloading {Settings.RuntimeUrl}");
            await DownloadTool(commonDirectory, Settings.RuntimeUrl).ConfigureAwait(false);
        }

        // Download Proton if it's missing, ensure the proton prefix, and return.
        if (Settings.IsProton)
        {
            if (!File.Exists(Settings.WinePath))
            {
                if (string.IsNullOrEmpty(Settings.DownloadUrl))
                    throw new FileNotFoundException($"Proton not found at the given path; {Settings.WinePath}, and no download url provided.");
                Log.Information($"Compatibility tool (Proton) does not exist. Downloading {Settings.DownloadUrl} to {compatToolsDirectory.FullName}");
                await DownloadTool(compatToolsDirectory, Settings.DownloadUrl).ConfigureAwait(false);
            }
            EnsurePrefix();
            UninstallNvngx();
            IsToolReady = true;
            return;
        }

        // Download Wine if it's missing
        if (!File.Exists(Settings.WinePath))
        {
            if (string.IsNullOrEmpty(Settings.DownloadUrl))
                throw new FileNotFoundException($"Wine not found at the given path: {Settings.WinePath}, and no download url provided.");
            Log.Information($"Compatibility tool (Wine) does not exist, downloading {Settings.DownloadUrl} to {wineDirectory.FullName}");
            await DownloadTool(wineDirectory, Settings.DownloadUrl).ConfigureAwait(false);
        }
        EnsurePrefix();
        
        // Download and install DXVK if enabled
        if (DxvkSettings.Enabled)
            await InstallDxvk().ConfigureAwait(false);

        if (DxvkSettings.NvapiEnabled)
            await InstallNvapi().ConfigureAwait(false);

        IsToolReady = true;
    }

    public async Task DownloadWine()
    {
        await DownloadTool(wineDirectory, Settings.DownloadUrl).ConfigureAwait(false);
    }

    public async Task DownloadProton()
    {
        await DownloadTool(compatToolsDirectory, Settings.DownloadUrl).ConfigureAwait(false);
    }

    public async Task DownloadRuntime()
    {
        await DownloadTool(commonDirectory, Settings.RuntimeUrl).ConfigureAwait(false);
    }

    public async Task DownloadDxvk()
    {
        await DownloadTool(dxvkDirectory, DxvkSettings.DownloadUrl).ConfigureAwait(false);
    }

    public async Task DownloadNvapi()
    {
        var nvapiFolder = new DirectoryInfo(Path.Combine(dxvkDirectory.FullName, DxvkSettings.NvapiFolderName));
        nvapiFolder.Create();
        await DownloadTool(nvapiFolder, DxvkSettings.NvapiDownloadUrl).ConfigureAwait(false);
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

    private async Task InstallNvapi()
    {
        var dxvkPath = Path.Combine(dxvkDirectory.FullName, DxvkSettings.NvapiFolderName, "x64");
        if (!Directory.Exists(dxvkPath))
        {
            Log.Information($"DXVK Nvapi does not exist, downloading {DxvkSettings.NvapiDownloadUrl}");
            var nvapiFolder = new DirectoryInfo(Path.Combine(dxvkDirectory.FullName, DxvkSettings.NvapiFolderName));
            nvapiFolder.Create();
            await DownloadTool(nvapiFolder, DxvkSettings.NvapiDownloadUrl).ConfigureAwait(false);
        }

        var system32 = Path.Combine(Settings.Prefix.FullName, "drive_c", "windows", "system32");
        var files = Directory.GetFiles(dxvkPath);

        foreach (string fileName in files)
        {
            File.Copy(fileName, Path.Combine(system32, Path.GetFileName(fileName)), true);
        }

        // Create symlinks to nvngx.dll and _nvngx.dll in the GamePath/game folder. For some reason it doesn't work if you put them in system32.
        // If NvngxOverride is set, assume the files/symlinks are already there. For Nix compatibility, mostly.
        if (!string.IsNullOrEmpty(DxvkSettings.NvngxFolder) && Directory.Exists(DxvkSettings.NvngxFolder) && !DxvkSettings.NvngxOverride)
        {
            string[] targets = { "nvngx.dll", "_nvngx.dll"};
            foreach (var target in targets)
            {
                var source = new FileInfo(Path.Combine(DxvkSettings.NvngxFolder, target));
                var destination = new FileInfo(Path.Combine(GamePath.FullName, "game", target));
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
                    Log.Error($"Missing Nvidia dll! DLSS may not work. {target} not found in {DxvkSettings.NvngxFolder}");
            }
        }

        // 32-bit files for Directx9. Only needed for external programs.
        var dxvkPath32 = Path.Combine(dxvkDirectory.FullName, DxvkSettings.NvapiFolderName, "x32");
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

    private void UninstallNvngx()
    {
        File.Delete(Path.Combine(GamePath.FullName, "game", "nvngx.dll"));
        File.Delete(Path.Combine(GamePath.FullName, "game", "_nvngx.dll"));
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
        bool runinprefix = true;
        // For proton, if the prefix hasn't been initialized, we need to use "proton run" instead of "proton runinprefix"
        // That will generate these files.
        if (!File.Exists(Path.Combine(Settings.Prefix.FullName, "config_info")) &&
            !File.Exists(Path.Combine(Settings.Prefix.FullName, "pfx.lock")) &&
            !File.Exists(Path.Combine(Settings.Prefix.FullName, "tracked_files")) &&
            !File.Exists(Path.Combine(Settings.Prefix.FullName, "version")))
        {
            runinprefix = false;
        }
        RunWithoutRuntime("cmd /c dir %userprofile%/Documents > nul", runinprefix, false).WaitForExit();
    }

    // This function exists to speed up launch times when using a Steam runtime. The runtime isn't needed
    // to do a few basic things like checking paths and initializing the prefix; it just slows things down considerably.
    // This can save upwards of 10 seconds, which otherwise might make the user think the launcher has hung or crashed.
    public Process RunWithoutRuntime(string command, bool runinprefix = true, bool redirect = true)
    {
        if (!Settings.IsProton)
            return RunInPrefix(command, redirectOutput: redirect, writeLog: redirect);
        var psi = new ProcessStartInfo(Settings.WinePath);
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        // Need to set these or proton will refuse to run.
        psi.Environment.Add("STEAM_COMPAT_DATA_PATH", Settings.Prefix.FullName);
        psi.Environment.Add("STEAM_COMPAT_CLIENT_INSTALL_PATH", compatToolsDirectory.Parent.FullName);
        // Need to properly set esync/fsync or it can sometimes cause problems with the wineserver.
        if (!Settings.FsyncOn)
        {
            psi.Environment.Add("PROTON_NO_FSYNC", "1");
            if (!Settings.EsyncOn)
                psi.Environment.Add("PROTON_NO_ESYNC", "1");
        }
        if (!DxvkSettings.Enabled)
            psi.Environment.Add("PROTON_USE_WINED3D", "1");

        var wineOverrides = $"msquic=,mscoree=n,b;d3d11,dxgi={(DxvkSettings.Enabled ? "n,b" : "b")}";
        if (!String.IsNullOrWhiteSpace(Settings.WineDLLOverrides))
            wineOverrides += (";" + Settings.WineDLLOverrides);
        psi.Environment.Add("WINEDLLOVERRIDES", wineOverrides);
        psi.Arguments = runinprefix ? Settings.RunInPrefix + command : Settings.Run + command;
        var quickRun = new Process();
        quickRun.StartInfo = psi;
        quickRun.Start();
        Log.Verbose("Running without runtime: {FileName} {Arguments}", psi.FileName, psi.Arguments);
        return quickRun;
    }

    public void RunExternalProgram(string command, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        // Just need to make sure prefix is ensured before running RunInPrefix() from XL.Core. Otherwise it can create a broken prefix.
        EnsurePrefix();
        RunInPrefix(command, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string command, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Settings.Runner);
        psi.Arguments = Settings.RunInRuntime + Settings.RunInPrefix + command;

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, psi.Arguments);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Settings.Runner);
        if (Settings.IsRuntime)
            foreach (var arg in Settings.RunInRuntimeArray)
                psi.ArgumentList.Add(arg);
        if (Settings.IsProton)
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
        if (Settings.IsRuntime)
        {
            var importantPaths = new System.Text.StringBuilder(GamePath.FullName + ":" + GameConfigPath.FullName);
            var steamCompatMounts = System.Environment.GetEnvironmentVariable("STEAM_COMPAT_MOUNTS");
            if (!string.IsNullOrEmpty(steamCompatMounts))
                importantPaths.Append(":" + steamCompatMounts.Trim(':'));
            
            // These paths are for winediscordipcbridge.exe. Note that exact files are being passed, not directories.
            // You can't pass the whole /run/user/<userid> directory; it will get ignored.
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
            
            wineEnvironmentVariables.Add("STEAM_COMPAT_MOUNTS", importantPaths.ToString());
        }
        
        if (Settings.IsProton)
        {
            // Need to set these or proton will refuse to run.
            wineEnvironmentVariables.Add("STEAM_COMPAT_DATA_PATH", Settings.Prefix.FullName);
            wineEnvironmentVariables.Add("STEAM_COMPAT_CLIENT_INSTALL_PATH", compatToolsDirectory.Parent.FullName);
            if (!Settings.FsyncOn)
            {
                wineEnvironmentVariables.Add("PROTON_NO_FSYNC", "1");
                if (!Settings.EsyncOn)
                    wineEnvironmentVariables.Add("PROTON_NO_ESYNC", "1");
            }
            if (wineD3D)
                wineEnvironmentVariables.Add("PROTON_USE_WINED3D", "1");
        }
        
        if (!Settings.IsProton)
        {
            wineEnvironmentVariables.Add("WINEESYNC", Settings.EsyncOn ? "1" : "0");
            wineEnvironmentVariables.Add("WINEFSYNC", Settings.FsyncOn ? "1" : "0");
            wineEnvironmentVariables.Add("WINEPREFIX", Settings.Prefix.FullName);
        }

        var wineOverrides = $"msquic=,mscoree=n,b;d3d11,dxgi={(wineD3D ? "b" : "n,b")}";
        if (!String.IsNullOrWhiteSpace(Settings.WineDLLOverrides))
            wineOverrides += (";" + Settings.WineDLLOverrides);
        wineEnvironmentVariables.Add("WINEDLLOVERRIDES", wineOverrides);

        if (!string.IsNullOrEmpty(Settings.DebugVars))
        {
            wineEnvironmentVariables.Add("WINEDEBUG", Settings.DebugVars);
        }

        wineEnvironmentVariables.Add("XL_WINEONLINUX", "true");

        if (this.gamemodeOn)
            wineEnvironmentVariables.Add("LD_PRELOAD", MergeLDPreload("libgamemodeauto.so.0" , Environment.GetEnvironmentVariable("LD_PRELOAD")));

        foreach (var dxvkVar in DxvkSettings.Environment)
        {
            // Don't add DXVK_ENABLE_NVAPI to the environment. Let Proton handle it.
            if (dxvkVar.Key == "DXVK_ENABLE_NVAPI" && Settings.IsProton)
                continue;
            wineEnvironmentVariables.Add(dxvkVar.Key, dxvkVar.Value);
        }
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
        var winePath = RunWithoutRuntime(launchArguments);
        var output = winePath.StandardOutput.ReadToEnd();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    public void AddRegistryKey(string key, string value, string data)
    {
        var args = $"reg add {key} /v {value} /d {data} /f";
        var wineProcess = RunWithoutRuntime(args);
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