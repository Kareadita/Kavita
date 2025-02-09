using System;
using System.IO;
using System.Text.Json;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Hosting;

namespace Kavita.Common;

public static class Configuration
{
    public const string DefaultIpAddresses = "0.0.0.0,::";
    public const string DefaultBaseUrl = "/";
    public const int DefaultHttpPort = 5000;
    public const int DefaultTimeOutSecs = 90;
    public const long DefaultCacheMemory = 75;
    private static readonly string AppSettingsFilename = Path.Join("config", GetAppSettingFilename());

    public static string KavitaPlusApiUrl = "https://plus.kavitareader.com";
    public static string StatsApiUrl = "https://stats.kavitareader.com";

    public static int Port
    {
        get => GetPort(GetAppSettingFilename());
        set => SetPort(GetAppSettingFilename(), value);
    }

    public static string IpAddresses
    {
        get => GetIpAddresses(GetAppSettingFilename());
        set => SetIpAddresses(GetAppSettingFilename(), value);
    }

    public static string JwtToken
    {
        get => GetJwtToken(GetAppSettingFilename());
        set => SetJwtToken(GetAppSettingFilename(), value);
    }

    public static string BaseUrl
    {
        get => GetBaseUrl(GetAppSettingFilename());
        set => SetBaseUrl(GetAppSettingFilename(), value);
    }

    public static long CacheSize
    {
        get => GetCacheSize(GetAppSettingFilename());
        set => SetCacheSize(GetAppSettingFilename(), value);
    }

    public static bool AllowIFraming => GetAllowIFraming(GetAppSettingFilename());

    private static string GetAppSettingFilename()
    {
        if (!string.IsNullOrEmpty(AppSettingsFilename))
        {
            return AppSettingsFilename;
        }

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isDevelopment = environment == Environments.Development;
        return "appsettings" + (isDevelopment ? ".Development" : string.Empty) + ".json";
    }

    #region JWT Token

    private static string GetJwtToken(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);
            return jsonObj.TokenKey;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading app settings: " + ex.Message);
        }

        return string.Empty;
    }

    private static void SetJwtToken(string filePath, string token)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);
            jsonObj.TokenKey = token;
            json = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception)
        {
            /* Swallow exception */
        }
    }

    public static bool CheckIfJwtTokenSet()
    {
        try
        {
            return !GetJwtToken(GetAppSettingFilename()).StartsWith("super secret unguessable key");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error writing app settings: " + ex.Message);
        }

        return false;
    }

    #endregion

    #region Port

    private static void SetPort(string filePath, int port)
    {
        if (OsInfo.IsDocker)
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);
            jsonObj.Port = port;
            json = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception)
        {
            /* Swallow Exception */
        }
    }

    private static int GetPort(string filePath)
    {
        if (OsInfo.IsDocker)
        {
            return DefaultHttpPort;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);
            return jsonObj.Port;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error writing app settings: " + ex.Message);
        }

        return DefaultHttpPort;
    }

    #endregion

    #region Ip Addresses

    private static void SetIpAddresses(string filePath, string ipAddresses)
    {
        if (OsInfo.IsDocker)
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);
            jsonObj.IpAddresses = ipAddresses;
            json = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception)
        {
            /* Swallow Exception */
        }
    }

    private static string GetIpAddresses(string filePath)
    {
        if (OsInfo.IsDocker)
        {
            return string.Empty;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);
            return jsonObj.IpAddresses;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error writing app settings: " + ex.Message);
        }

        return string.Empty;
    }
    #endregion

    #region BaseUrl
    private static string GetBaseUrl(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);

            var baseUrl = jsonObj.BaseUrl;
            if (!string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = UrlHelper.EnsureStartsWithSlash(baseUrl);
                baseUrl = UrlHelper.EnsureEndsWithSlash(baseUrl);

                return baseUrl;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading app settings: " + ex.Message);
        }

        return DefaultBaseUrl;
    }

    private static void SetBaseUrl(string filePath, string value)
    {

        var baseUrl = !value.StartsWith('/')
            ? $"/{value}"
            : value;

        baseUrl = !baseUrl.EndsWith('/')
                    ? $"{baseUrl}/"
                    : baseUrl;

        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);
            jsonObj.BaseUrl = baseUrl;
            json = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception)
        {
            /* Swallow exception */
        }
    }
    #endregion

    #region CacheSize
    private static void SetCacheSize(string filePath, long cache)
    {
        if (cache <= 0) return;
        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);
            jsonObj.Cache = cache;
            json = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception)
        {
            /* Swallow Exception */
        }
    }

    private static long GetCacheSize(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);

            return jsonObj.Cache == 0 ? DefaultCacheMemory : jsonObj.Cache;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error writing app settings: " + ex.Message);
        }

        return DefaultCacheMemory;
    }


    #endregion

    #region AllowIFraming
    private static bool GetAllowIFraming(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<AppSettings>(json);
            return jsonObj.AllowIFraming;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading app settings: " + ex.Message);
        }

        return false;
    }
    #endregion

    private sealed class AppSettings
    {
        public string TokenKey { get; set; }
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public int Port { get; set; } = DefaultHttpPort;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public string IpAddresses { get; set; } = string.Empty;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public string BaseUrl { get; set; }
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public long Cache { get; set; } = DefaultCacheMemory;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public bool AllowIFraming { get; set; } = false;
    }
}
