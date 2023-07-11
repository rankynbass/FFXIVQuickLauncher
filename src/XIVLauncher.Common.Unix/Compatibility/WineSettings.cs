using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility;

public class WineSettings
{
    public string RunCommand { get; set; }

    public string RunArguments { get; }

    public string MinimalRunCommand { get; }

    public string ProtonVerb => "runinprefix";

    public string WineServer { get; }

    public string PathArguments { get; }

    public string Folder { get; }

    public string DownloadUrl { get; }

    private string xlcoreFolder;

    public bool IsProton { get; }

    public bool IsManaged { get; }

    public Dictionary<string, string> Environment { get; }

    public WineSettings(string customWine, string customWineArgs, string folder, string url, string rootFolder, Dictionary<string, string> env = null, bool isProton = false, string minRunCmd = "")
    {
        RunArguments = customWineArgs;
        Folder = folder;
        DownloadUrl = url;
        xlcoreFolder = rootFolder;
        Environment = (env is null) ? new Dictionary<string, string>() : env;
        IsProton = isProton;
        if (string.IsNullOrEmpty(customWine))
        {
            var wineBinPath = Path.Combine(Path.Combine(rootFolder, "compatibilitytool", "wine"), folder, "bin");
            RunCommand = SetWineOrWine64(wineBinPath);
            WineServer = Path.Combine(wineBinPath, "wineserver");
            IsManaged = true;
        }
        else
        {
            if (isProton)
            {
                var command = new FileInfo(customWine);
                RunCommand = command.FullName;
                WineServer = Path.Combine(command.DirectoryName, "files", "bin", "wineserver");
            }
            else
            {
                RunCommand = SetWineOrWine64(customWine);
                WineServer = Path.Combine(customWine, "wineserver");
                IsManaged = false;
            }
        }
        MinimalRunCommand = string.IsNullOrEmpty(minRunCmd) ? RunCommand : minRunCmd;
    }

    public string SetWineOrWine64(string path)
    {
        if (File.Exists(Path.Combine(path, "wine64")))
            return Path.Combine(path, "wine64");

        if (File.Exists(Path.Combine(path, "wine")))
            return Path.Combine(path, "wine");
            
        return string.Empty;
    }
}