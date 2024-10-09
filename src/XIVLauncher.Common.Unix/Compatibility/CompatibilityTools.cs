﻿using System;
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

    public GameSettings Game { get; private set; }

    public RunnerSettings Runner { get; private set; }

    public DxvkSettings Dxvk { get; private set; }

    public DLSSSettings DLSS { get; private set; }

    public bool IsToolDownloaded => File.Exists(Runner.RunnerPath) && Runner.Prefix.Exists;

    public CompatibilityTools(GameSettings gameSettings, RunnerSettings runnerSettings, DxvkSettings dxvkSettings, DLSSSettings dlssSettings)
    {
        this.Game = gameSettings;
        this.Runner = runnerSettings;
        this.Dxvk = dxvkSettings;
        this.DLSS = dlssSettings;

        this.wineDirectory = new DirectoryInfo(Path.Combine(Game.ToolsFolder.FullName, "wine"));
        this.dxvkDirectory = new DirectoryInfo(Path.Combine(Game.ToolsFolder.FullName, "dxvk"));
        this.compatToolsDirectory = new DirectoryInfo(Path.Combine(Game.SteamFolder.FullName, "compatibilitytools.d"));
        this.commonDirectory = new DirectoryInfo(Path.Combine(Game.SteamFolder.FullName, "steamapps", "common"));

        this.logWriter = new StreamWriter(Runner.LogFile.FullName);

        if (!this.wineDirectory.Exists)
            this.wineDirectory.Create();

        if (!this.dxvkDirectory.Exists)
            this.dxvkDirectory.Create();

        if (!this.compatToolsDirectory.Exists && Runner.IsProton)
            this.compatToolsDirectory.Create();

        if (!this.commonDirectory.Exists && Runner.IsUsingRuntime)
            this.commonDirectory.Create();

        if (!Runner.Prefix.Exists)
        {
            Runner.Prefix.Create();
            if (Runner.IsProton)
                File.CreateSymbolicLink(Path.Combine(Runner.Prefix.FullName, "pfx"), Runner.Prefix.FullName);
        }
        else
        {
            if (File.Exists(Path.Combine(Runner.Prefix.FullName, "pfx")))
                File.Delete(Path.Combine(Runner.Prefix.FullName, "pfx"));
            if (Runner.IsProton && !Directory.Exists(Path.Combine(Runner.Prefix.FullName, "pfx")))
                File.CreateSymbolicLink(Path.Combine(Runner.Prefix.FullName, "pfx"), Runner.Prefix.FullName);
        }
    }

    public async Task EnsureTool(DirectoryInfo tempPath)
    {
        // Download the container if it's missing
        if (Runner.IsUsingRuntime && !File.Exists(Runner.Command))
        {
            if (string.IsNullOrEmpty(Runner.RuntimeUrl))
                throw new FileNotFoundException($"Steam runtime selected, but is not present, and no download url provided.");
            Log.Information($"Steam Linux Runtime does not exist, downloading {Runner.RuntimeUrl}");
            await DownloadRuntime().ConfigureAwait(false);
        }

        // Download Proton if it's missing, ensure the proton prefix, and return.
        if (Runner.IsProton)
        {
            if (!File.Exists(Runner.RunnerPath))
            {
                if (string.IsNullOrEmpty(Runner.DownloadUrl))
                    throw new FileNotFoundException($"Proton not found at the given path; {Runner.RunnerPath}, and no download url provided.");
                Log.Information($"Compatibility tool (Proton) does not exist. Downloading {Runner.DownloadUrl} to {compatToolsDirectory.FullName}");
                await DownloadProton().ConfigureAwait(false);
            }
            EnsurePrefix();
            IsToolReady = true;
            return;
        }

        // Download Wine if it's missing
        if (!File.Exists(Runner.RunnerPath))
        {
            if (string.IsNullOrEmpty(Runner.DownloadUrl))
                throw new FileNotFoundException($"Wine not found at the given path: {Runner.RunnerPath}, and no download url provided.");
            Log.Information($"Compatibility tool (Wine) does not exist, downloading {Runner.DownloadUrl} to {wineDirectory.FullName}");
            await DownloadWine().ConfigureAwait(false);
        }
        EnsurePrefix();
        
        // Download and install DXVK if enabled
        if (Dxvk.Enabled)
        {
            await Dxvk.Install(dxvkDirectory, Runner.Prefix).ConfigureAwait(false);
        }
        if (DLSS.Enabled)
        {
            if (!DLSS.NoOverwrite)
            {
                DLSS.InstallNvidaFiles(Game.GameFolder);
            }
            await DLSS.Install(dxvkDirectory, Runner.Prefix).ConfigureAwait(false);
        }

        IsToolReady = true;
    }

    public async Task DownloadWine()
    {
        await DownloadTool(wineDirectory, Runner.DownloadUrl).ConfigureAwait(false);
    }

    public async Task DownloadProton()
    {
        await DownloadTool(compatToolsDirectory, Runner.DownloadUrl).ConfigureAwait(false);
    }

    public async Task DownloadRuntime()
    {
        await DownloadTool(commonDirectory, Runner.RuntimeUrl).ConfigureAwait(false);
    }

    public async Task DownloadDxvk()
    {
        await DownloadTool(dxvkDirectory, Dxvk.DownloadUrl).ConfigureAwait(false);
    }

    public async Task DownloadNvapi()
    {
        var nvapiFolder = new DirectoryInfo(Path.Combine(dxvkDirectory.FullName, DLSS.FolderName));
        nvapiFolder.Create();
        await DownloadTool(nvapiFolder, DLSS.DownloadUrl).ConfigureAwait(false);
    }

    internal static async Task DownloadTool(DirectoryInfo installDirectory, string downloadUrl)
    {
        if (string.IsNullOrEmpty(downloadUrl))
            throw new ArgumentException("Empty or null string passed as url.");
        using var client = new HttpClient();
        var tempPath = Path.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(downloadUrl));
        PlatformHelpers.Untar(tempPath, installDirectory.FullName);

        File.Delete(tempPath);
    }

    private void ResetPrefix()
    {
        Runner.Prefix.Refresh();

        if (Runner.Prefix.Exists)
            Runner.Prefix.Delete(true);

        Runner.Prefix.Create();
        if (Runner.IsProton)
            File.CreateSymbolicLink(Path.Combine(Runner.Prefix.FullName, "pfx"), Runner.Prefix.FullName);

        EnsurePrefix();
    }

    public void EnsurePrefix()
    {
        bool runinprefix = true;
        // For proton, if the prefix hasn't been initialized, we need to use "proton run" instead of "proton runinprefix"
        // That will generate these files.
        if (!File.Exists(Path.Combine(Runner.Prefix.FullName, "config_info")) &&
            !File.Exists(Path.Combine(Runner.Prefix.FullName, "pfx.lock")) &&
            !File.Exists(Path.Combine(Runner.Prefix.FullName, "tracked_files")) &&
            !File.Exists(Path.Combine(Runner.Prefix.FullName, "version")))
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
        if (!Runner.IsProton)
            return RunInPrefix(command, redirectOutput: redirect, writeLog: redirect);
        var psi = new ProcessStartInfo(Runner.RunnerPath);
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        // Need to set these or proton will refuse to run.
        psi.Environment.Add("STEAM_COMPAT_DATA_PATH", Runner.Prefix.FullName);
        psi.Environment.Add("STEAM_COMPAT_CLIENT_INSTALL_PATH", compatToolsDirectory.Parent.FullName);
        // Need to properly set esync/fsync or it can sometimes cause problems with the wineserver.
        if (!Runner.FsyncOn)
        {
            psi.Environment.Add("PROTON_NO_FSYNC", "1");
            if (!Runner.EsyncOn)
                psi.Environment.Add("PROTON_NO_ESYNC", "1");
        }
        if (!Dxvk.Enabled)
            psi.Environment.Add("PROTON_USE_WINED3D", "1");

        psi.Environment.Add("WINEDLLOVERRIDES", Runner.GetWineDLLOverrides(Dxvk.Enabled));
        
        psi.Arguments = runinprefix ? Runner.RunInPrefixVerb + command : Runner.RunVerb + command;
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
        var psi = new ProcessStartInfo(Runner.Command);
        psi.Arguments = Runner.RunInRuntimeArguments + Runner.RunInPrefixVerb + command;

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, psi.Arguments);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Runner.Command);
        if (Runner.IsUsingRuntime)
            foreach (var arg in Runner.RunInRuntimeArgumentsArray)
                psi.ArgumentList.Add(arg);
        if (Runner.IsProton)
            psi.ArgumentList.Add(Runner.RunInPrefixVerb.Trim());
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


    private Process RunInPrefix(ProcessStartInfo psi, string workingDirectory, IDictionary<string, string> environment, bool redirectOutput, bool writeLog, bool wineD3D)
    {
        psi.RedirectStandardOutput = redirectOutput;
        psi.RedirectStandardError = writeLog;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workingDirectory;

        var wineEnvironmentVariables = new Dictionary<string, string>();
        if (Runner.IsUsingRuntime)
        {
            var importantPaths = new System.Text.StringBuilder(Game.GameFolder.FullName + ":" + Game.ConfigFolder.FullName);
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
        
        if (Runner.IsProton)
        {
            // Need to set these or proton will refuse to run.
            wineEnvironmentVariables.Add("STEAM_COMPAT_DATA_PATH", Runner.Prefix.FullName);
            wineEnvironmentVariables.Add("STEAM_COMPAT_CLIENT_INSTALL_PATH", compatToolsDirectory.Parent.FullName);
            if (!Runner.FsyncOn)
            {
                wineEnvironmentVariables.Add("PROTON_NO_FSYNC", "1");
                if (!Runner.EsyncOn)
                    wineEnvironmentVariables.Add("PROTON_NO_ESYNC", "1");
            }
            if (!Dxvk.Enabled || wineD3D)
                wineEnvironmentVariables.Add("PROTON_USE_WINED3D", "1");
        }
        
        if (!Runner.IsProton)
        {
            wineEnvironmentVariables.Add("WINEESYNC", Runner.EsyncOn ? "1" : "0");
            wineEnvironmentVariables.Add("WINEFSYNC", Runner.FsyncOn ? "1" : "0");
            wineEnvironmentVariables.Add("WINEPREFIX", Runner.Prefix.FullName);
        }

        wineEnvironmentVariables.Add("WINEDLLOVERRIDES", Runner.GetWineDLLOverrides(Dxvk.Enabled && !wineD3D));

        if (!string.IsNullOrEmpty(Runner.DebugVars))
        {
            wineEnvironmentVariables.Add("WINEDEBUG", Runner.DebugVars);
        }

        wineEnvironmentVariables.Add("XL_WINEONLINUX", "true");

        if (Game.GameModeEnabled)
            wineEnvironmentVariables.Add("LD_PRELOAD", MergeLDPreload("libgamemodeauto.so.0" , Environment.GetEnvironmentVariable("LD_PRELOAD")));

        MergeDictionaries(psi.Environment, wineEnvironmentVariables);
        MergeDictionaries(psi.Environment, Dxvk.Environment);
        MergeDictionaries(psi.Environment, DLSS.Environment);
        MergeDictionaries(psi.Environment, Game.Environment);       // Allow extra environment vars to override everything else.
        MergeDictionaries(psi.Environment, environment);

#if FLATPAK_NOTRIGHTNOW
        psi.FileName = "flatpak-spawn";

        psi.ArgumentList.Insert(0, "--host");
        psi.ArgumentList.Insert(1, Runner.RunnerPath);

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
        var psi = new ProcessStartInfo(Runner.WineServerPath)
        {
            Arguments = "-k"
        };
        psi.EnvironmentVariables.Add("WINEPREFIX", Runner.Prefix.FullName);

        Process.Start(psi);
    }
}