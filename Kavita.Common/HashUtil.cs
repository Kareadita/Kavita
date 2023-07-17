using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DeviceId;
using DeviceId.Components;
using Kavita.Common.EnvironmentInfo;

namespace Kavita.Common;

public static class HashUtil
{
    private static string CalculateCrc(string input)
    {
        var mCrc = 0xffffffff;
        var bytes = Encoding.UTF8.GetBytes(input);
        foreach (var myByte in bytes)
        {
            mCrc ^=  (uint)myByte << 24;
            for (var i = 0; i < 8; i++)
            {
                if ((Convert.ToUInt32(mCrc) & 0x80000000) == 0x80000000)
                {
                    mCrc = (mCrc << 1) ^ 0x04C11DB7;
                }
                else
                {
                    mCrc <<= 1;
                }
            }
        }

        return $"{mCrc:x8}";
    }

    /// <summary>
    /// Calculates a unique, Anonymous Token that will represent this unique Kavita installation.
    /// </summary>
    /// <returns></returns>
    public static string AnonymousToken()
    {
        var seed = $"{Environment.ProcessorCount}_{Environment.OSVersion.Platform}_{Configuration.JwtToken}_{Environment.UserName}";
        return CalculateCrc(seed);
    }

    public static string ServerToken()
    {
        var seed = new DeviceIdBuilder()
            .AddMacAddress()
            .AddUserName()
            .AddComponent("ProcessorCount", new DeviceIdComponent($"{Environment.ProcessorCount}"))
            .AddComponent("OSPlatform", new DeviceIdComponent($"{Environment.OSVersion.Platform}"))
            .OnWindows(windows => windows
                .AddProcessorId())
            .OnLinux(linux =>
            {
                var osInfo = RunAndCapture("uname", "-a");
                if (Regex.IsMatch(osInfo, @"\bUnraid\b"))
                {
                    var cpuModel = RunAndCapture("lscpu", string.Empty);
                    var match = Regex.Match(cpuModel, @"Model name:\s+(.+)");
                    linux.AddComponent("CPUModel", new DeviceIdComponent($"{match.Groups[1].Value.Trim()}"));
                    return;
                }
                linux.AddMotherboardSerialNumber();
            })
            .OnMac(mac => mac.AddSystemDriveSerialNumber())
            .ToString();
        return CalculateCrc(seed);
    }

    /// <summary>
    /// Generates a unique API key to this server instance
    /// </summary>
    /// <returns></returns>
    public static string ApiKey()
    {
        var id = Guid.NewGuid();
        if (id.Equals(Guid.Empty))
        {
            id = Guid.NewGuid();
        }

        return id.ToString();
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
