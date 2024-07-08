using System;
using System.Diagnostics;

namespace Kavita.Common.EnvironmentInfo;

public static class OsInfo
{
    public static Os Os { get; }
    public static bool IsNotWindows => !IsWindows;
    public static bool IsLinux => Os is Os.Linux or Os.LinuxMusl or Os.Bsd;
    public static bool IsOsx => Os == Os.Osx;
    public static bool IsWindows => Os == Os.Windows;
    public static bool IsDocker =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
        Environment.GetEnvironmentVariable("LSIO_FIRST_PARTY") == "true";

    static OsInfo()
    {
        var platform = Environment.OSVersion.Platform;

        switch (platform)
        {
            case PlatformID.Win32NT:
            {
                Os = Os.Windows;
                break;
            }

            case PlatformID.MacOSX:
            case PlatformID.Unix:
            {
                Os = GetPosixFlavour();
                break;
            }
        }
    }

    private static Os GetPosixFlavour()
    {
        var output = RunAndCapture("uname", "-s");

        if (output.StartsWith("Darwin"))
        {
            return Os.Osx;
        }
        else if (output.Contains("BSD"))
        {
            return Os.Bsd;
        }
        else
        {
#if ISMUSL
                return Os.LinuxMusl;
#else
            return Os.Linux;
#endif
        }
    }

    private static string RunAndCapture(string filename, string args)
    {
        var p = new Process
        {
            StartInfo =
            {
                FileName = filename,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            }
        };

        p.Start();

        // To avoid deadlocks, always read the output stream first and then wait.
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(1000);

        return output;
    }
}


public enum Os
{
    Windows,
    Linux,
    Osx,
    LinuxMusl,
    Bsd
}
