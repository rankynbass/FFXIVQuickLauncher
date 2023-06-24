﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Util;

#if FLATPAK
#warning THIS IS A FLATPAK BUILD!!!
#endif

namespace XIVLauncher.Common.Unix.Compatibility;

public class CompatibilityTools
{
    private DirectoryInfo wineDirectory;

    private DirectoryInfo dxvkDirectory;

    private StreamWriter logWriter;

    public bool IsToolReady { get; private set; }

    public WineRunner WineSettings { get; private set; }

    public DxvkRunner DxvkSettings { get; private set; }

    private Dictionary<string, string> EnvVars;

    public string WineDLLOverrides;

    private FileInfo LogFile;

    public DirectoryInfo Prefix { get; private set; }

    public bool IsToolDownloaded => File.Exists(WineSettings.RunCommand) && Prefix.Exists;


    public CompatibilityTools(WineRunner wineSettings, DxvkRunner dxvkSettings, Dictionary<string, string> environment, string wineoverrides, DirectoryInfo prefix, DirectoryInfo toolsFolder, FileInfo logfile)
    {
        WineSettings = wineSettings;
        DxvkSettings = dxvkSettings;
        EnvVars = environment;
        Prefix = prefix;
        WineDLLOverrides = (string.IsNullOrEmpty(wineoverrides)) ? "msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi=n,b" : wineoverrides;
        wineDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "beta"));
        dxvkDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "dxvk"));
        LogFile = logfile;

        logWriter = new StreamWriter(LogFile.FullName);

        if (!wineDirectory.Exists)
            wineDirectory.Create();

        if (!dxvkDirectory.Exists)
            dxvkDirectory.Create();

        if (!Prefix.Exists)
            Prefix.Create();
    }

    public async Task EnsureTool(DirectoryInfo tempPath)
    {
        if (WineSettings.IsProton)
        {
            IsToolReady = true;
            EnsurePrefix();
            return;
        }

        // Check to make sure wine is valid
        await WineSettings.Install();
        if (!File.Exists(WineSettings.RunCommand))
            throw new FileNotFoundException("The wine64 binary was not found.");
        EnsurePrefix();

        // Check to make sure dxvk is valid
        if (DxvkSettings.IsDxvk && !WineSettings.IsProton)
            await DxvkSettings.Install();

        IsToolReady = true;
    }

    private void ResetPrefix()
    {
        Prefix.Refresh();

        if (Prefix.Exists)
            Prefix.Delete(true);

        Prefix.Create();
        EnsurePrefix();
    }

    public void EnsurePrefix()
    {
        if (WineSettings.IsProton)
            RunInMinProton("run", "cmd /c dir %userprofile%/Documents > nul").WaitForExit();
        else
            RunInPrefix("cmd /c dir %userprofile%/Documents > nul").WaitForExit();
    }

    public Process RunInMinProton(string verb, string command, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(WineSettings.MinimalRunCommand);
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.Environment.Add("STEAM_COMPAT_DATA_PATH", WineSettings.Environment["STEAM_COMPAT_DATA_PATH"]);
        psi.Environment.Add("STEAM_COMPAT_CLIENT_INSTALL_PATH", WineSettings.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"]);
        if (wineD3D)
            psi.Environment.Add("WINEDLLOVERRIDES", "msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi=b");
        else
            psi.Environment.Add("WINEDLLOVERRIDES", WineDLLOverrides);
        psi.Arguments = CompactString(verb + " " + command);
       
        var minProton = new Process();
        minProton.StartInfo = psi;
        minProton.Start();
        Log.Information($"Running minimal proton in prefix: {psi.FileName} {psi.Arguments}");
        return minProton;
    }

    public Process RunInPrefix(string command, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(WineSettings.RunCommand);
        psi.Arguments = CompactString(WineSettings.RunArguments + (WineSettings.IsProton ? " runinprefix " : " ") + command);

        Log.Information("Running in prefix (string): {FileName} {Arguments}", psi.FileName, psi.Arguments);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(WineSettings.RunCommand);
        if (!string.IsNullOrEmpty(WineSettings.RunArguments))
            foreach (var param in WineSettings.RunArguments.Split(null))
                if (!string.IsNullOrEmpty(param)) psi.ArgumentList.Add(param);
        if (WineSettings.IsProton) psi.ArgumentList.Add("runinprefix");
        foreach (var arg in args)
            if (!string.IsNullOrEmpty(arg)) psi.ArgumentList.Add(arg);

        Log.Information("Running in prefix (array): {FileName} {Arguments}", psi.FileName, psi.ArgumentList.Aggregate(string.Empty, (a, b) => a + " " + b));
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
        var alist = a.Split(':');
        var blist = b.Split(':');
        var clist = (System.Environment.GetEnvironmentVariable("LD_PRELOAD") ?? "").Split(':');
        
        var merged = (alist.Union(blist)).Union(clist);

        var ldpreload = "";
        foreach (var item in merged)
            ldpreload += item + ":";
        
        return ldpreload.TrimEnd(':');
    }

    private string CompactString(string s)
    {
        var dst = new char[s.Length];
        uint end = 0;
        char prev = char.MinValue;
        for (int k = 0; k < s.Length; ++k)
        {
            var c = s[k];
            dst[end] = c;

            // We'll move forward if the current character is not ' ' or if prev char is not ' '
            // To avoid 'if' let's get diffs for c and prev and then use bitwise operatios to get 
            // 0 if n is 0 or 1 if n is non-zero
            uint x = (uint)(' ' - c) + (uint)(' ' - prev); // non zero if any non-zero

            end += ((x | (~x + 1)) >> 31) & 1; // https://stackoverflow.com/questions/3912112/check-if-a-number-is-non-zero-using-bitwise-operators-in-c by ruslik
            prev = c;
        }
        return new string(dst, 0, (int)end);
    }

    private Process RunInPrefix(ProcessStartInfo psi, string workingDirectory, IDictionary<string, string> environment, bool redirectOutput, bool writeLog, bool wineD3D)
    {
        psi.RedirectStandardOutput = redirectOutput;
        psi.RedirectStandardError = writeLog;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workingDirectory;

        var wineEnvironmentVariables = new Dictionary<string, string>();
        if (wineD3D)
            wineEnvironmentVariables.Add("WINEDLLOVERRIDES", "msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi=b");
        else
            wineEnvironmentVariables.Add("WINEDLLOVERRIDES", WineDLLOverrides);
        wineEnvironmentVariables.Add("XL_WINEONLINUX", "true");

        MergeDictionaries(psi.Environment, WineSettings.Environment);
        MergeDictionaries(psi.Environment, DxvkSettings.Environment);
        MergeDictionaries(psi.Environment, wineEnvironmentVariables);
        MergeDictionaries(psi.Environment, environment);
        Log.Verbose("Launching with the following environment:");
        foreach (var kvp in psi.Environment)
            Log.Verbose(kvp.Key + "=" + kvp.Value);

#if FLATPAK_NOTRIGHTNOW
        psi.FileName = "flatpak-spawn";

        psi.ArgumentList.Insert(0, "--host");
        psi.ArgumentList.Insert(1, WineSettings.RunCommand);

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
        var wineDbg = RunInPrefix("winedbg --command \"info procmap\"", redirectOutput: true);
        var output = wineDbg.StandardOutput.ReadToEnd();
        if (output.Contains("syntax error\n"))
            return 0;
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Where(
            l => int.Parse(l.Substring(1, 8), System.Globalization.NumberStyles.HexNumber) == winePid);
        var unixPids = matchingLines.Select(l => int.Parse(l.Substring(10, 8), System.Globalization.NumberStyles.HexNumber)).ToArray();
        return unixPids.FirstOrDefault();
    }

    public Int32 GetUnixProcessIdByName(string executableName)
    {
        int closest = 0;
        var currentProcess = Process.GetCurrentProcess(); // Gets XIVLauncher.Core's process
        bool nonunique = false;
        foreach (var process in Process.GetProcessesByName(executableName))
        {
            if (process.Id < currentProcess.Id)
                continue;  // Process was launched before XIVLauncher.Core

            // Assume that the closest PID to XIVLauncher.Core's is the correct one. But log an error if more than one is found.
            if ((closest - currentProcess.Id) > (process.Id - currentProcess.Id) || closest == 0)
            {
                if (closest != 0) nonunique = true;
                closest = process.Id;
            }
            if (nonunique) Log.Error($"More than one {executableName} found! Selecting the most likely match with process id {closest}.");
        }
        if (closest != 0) Log.Information($"Process for {executableName} found using fallback method.");
        return closest;
    }

    public string UnixToWinePath(string unixPath)
    {
        //var launchArguments = new string[] { "winepath", "--windows", unixPath };
        var winePath = (WineSettings.IsProton) ? RunInMinProton("getcompatpath", $"\"{unixPath}\"") : RunInPrefix(new string[] { "winepath", "--windows", unixPath }, redirectOutput: true);
        var output = winePath.StandardOutput.ReadToEnd();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    public void AddRegistryKey(string key, string value, string data)
    {
        var args = new string[] { "reg", "add", key, "/v", value, "/d", data, "/f" };
        var wineProcess = RunInPrefix(args);
        wineProcess.WaitForExit();
    }

    public void Kill()
    {
        var psi = new ProcessStartInfo(WineSettings.WineServer)
        {
            Arguments = "-k"
        };
        psi.Environment.Add("WINEPREFIX", WineSettings.GetWinePrefix());

        Process.Start(psi);
    }

    public static bool IsDirectoryEmpty(string folder)
    {
        if (!Directory.Exists(folder)) return true;
        return !Directory.EnumerateFileSystemEntries(folder).Any();
    }
}