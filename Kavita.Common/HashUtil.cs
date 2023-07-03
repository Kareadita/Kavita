using System;
using System.Text;
using DeviceId;
using DeviceId.Components;

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
            .AddOsVersion()
            .AddComponent("ProcessorCount", new DeviceIdComponent($"{Environment.ProcessorCount}"))
            .OnWindows(windows => windows
                .AddSystemUuid()
                .AddMotherboardSerialNumber()
                .AddSystemDriveSerialNumber())
            .OnLinux(linux => linux
                .AddMotherboardSerialNumber())
            .OnMac(mac => mac
                .AddSystemDriveSerialNumber()
                .AddPlatformSerialNumber()) // On Docker, this is the same as SystemDriveSerialNumber
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
}
