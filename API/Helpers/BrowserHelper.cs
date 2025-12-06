using System;
using API.Entities.Enums;

namespace API.Helpers;

/// <summary>
/// Handles all things around Parsing Headers
/// </summary>
public static class BrowserHelper
{
    public static ClientDeviceType DetermineClientType(string userAgent, string? endpoint = null)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return ClientDeviceType.Unknown;
        }

        var ua = userAgent.ToLowerInvariant();

        // ua contains "web-app" keyword, it's Kavita web app
        if (ua.Contains("web-app")) return ClientDeviceType.WebApp;
        if (ua.Contains("koreader") || ua.Contains("kobo touch")) return ClientDeviceType.KoReader;
        if (ua.Contains("panels")) return ClientDeviceType.Panels;
        if (ua.Contains("librera")) return ClientDeviceType.Librera;

        // Ensure we test everything else before web browsers, as all UAs will have web browser info
        // If this is an opds url, and it's not a custom server, then return as generic
        if (!string.IsNullOrEmpty(endpoint) && endpoint.Contains("/opds/"))
        {
            return ClientDeviceType.OpdsClient;
        }

        // Web browsers
        if (ua.Contains("chrome") || ua.Contains("firefox") ||
            ua.Contains("safari") || ua.Contains("edge"))
        {
            return ClientDeviceType.WebBrowser;
        }


        return ClientDeviceType.Unknown;
    }

    public static ClientDevicePlatform DetectPlatform(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return ClientDevicePlatform.Unknown;
        }

        var ua = userAgent.ToLowerInvariant();

        if (ua.Contains("windows") || ua.Contains("win32") || ua.Contains("win64"))
            return ClientDevicePlatform.Windows;
        if (ua.Contains("macintosh"))
            return ClientDevicePlatform.MacOs;
        if (ua.Contains("iphone") || ua.Contains("ipad") || ua.Contains("ipod") || ua.Contains("mac os"))
            return ClientDevicePlatform.Ios;

        // Linux and Android are easy to mix-up, we need to use some extra logic
        if (ua.Contains("android"))
            return ClientDevicePlatform.Android;
        if (ua.Contains("linux") && ua.Contains("ubuntu"))
            return ClientDevicePlatform.Linux;
        if (ua.Contains("linux") && !ua.Contains("android"))
            return ClientDevicePlatform.Linux;

        return ClientDevicePlatform.Unknown;
    }

    /// <summary>
    /// Attempts to derive DeviceType based on Platform and ClientType
    /// </summary>
    /// <param name="type"></param>
    /// <param name="platform"></param>
    /// <returns></returns>
    public static string CoaxDeviceType(ClientDeviceType type, ClientDevicePlatform platform)
    {
        return type switch
        {
            ClientDeviceType.KoReader or ClientDeviceType.Panels or ClientDeviceType.Librera =>
                platform is ClientDevicePlatform.Android or ClientDevicePlatform.Ios ? "Mobile" : "Desktop",
            _ => string.Empty
        };
    }
}
