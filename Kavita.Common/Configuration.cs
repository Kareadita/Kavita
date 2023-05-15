using System;
using System.IO;
using System.Text.Json;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Hosting;

namespace Kavita.Common;

public static class Configuration
{
    public const string DefaultIpAddresses = "0.0.0.0,::";
    public const string DefaultBaseUrl = "/";
    public const string DefaultXFrameOptions = "SAMEORIGIN";
    private static readonly string AppSettingsFilename = Path.Join("config", GetAppSettingFilename());

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

    public static string XFrameOptions => GetXFrameOptions(GetAppSettingFilename());

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
            var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
            const string key = "TokenKey";

            if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
            {
                return tokenElement.GetString();
            }

            return string.Empty;
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
            return GetJwtToken(GetAppSettingFilename()) != "super secret unguessable key";
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
        if (new OsInfo().IsDocker)
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
        const int defaultPort = 5000;
        if (new OsInfo().IsDocker)
        {
            return defaultPort;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
            const string key = "Port";

            if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
            {
                return tokenElement.GetInt32();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error writing app settings: " + ex.Message);
        }

        return defaultPort;
    }

    #endregion

    #region Ip Addresses

    private static void SetIpAddresses(string filePath, string ipAddresses)
    {
        if (new OsInfo().IsDocker)
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
        if (new OsInfo().IsDocker)
        {
            return string.Empty;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
            const string key = "IpAddresses";

            if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
            {
                return tokenElement.GetString();
            }
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
            var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
            const string key = "BaseUrl";

            if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
            {
                var baseUrl = tokenElement.GetString();
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    baseUrl = !baseUrl.StartsWith("/")
                                ? $"/{baseUrl}"
                                : baseUrl;

                    baseUrl = !baseUrl.EndsWith("/")
                                ? $"{baseUrl}/"
                                : baseUrl;

                    return baseUrl;
                }
                return DefaultBaseUrl;
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

        var baseUrl = !value.StartsWith("/")
            ? $"/{value}"
            : value;

        baseUrl = !baseUrl.EndsWith("/")
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

    #region XFrameOrigins
    private static string GetXFrameOptions(string filePath)
    {
        if (new OsInfo().IsDocker)
        {
            return DefaultBaseUrl;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
            const string key = "XFrameOrigins";

            if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
            {
                var origins = tokenElement.GetString();
                return !string.IsNullOrEmpty(origins) ? origins : DefaultBaseUrl;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading app settings: " + ex.Message);
        }

        return DefaultXFrameOptions;
    }
    #endregion

    private sealed class AppSettings
    {
        public string TokenKey { get; set; }
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public int Port { get; set; }
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public string IpAddresses { get; set; } = string.Empty;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public string BaseUrl { get; set; } = DefaultBaseUrl;
    }
}
