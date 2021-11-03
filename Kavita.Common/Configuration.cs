using System;
using System.IO;
using System.Text.Json;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Hosting;

namespace Kavita.Common
{
    public static class Configuration
    {
        public static readonly string AppSettingsFilename = Path.Join("config", GetAppSettingFilename());

        public static string Branch
        {
            get => GetBranch(GetAppSettingFilename());
            set => SetBranch(GetAppSettingFilename(), value);
        }

        public static int Port
        {
            get => GetPort(GetAppSettingFilename());
            set => SetPort(GetAppSettingFilename(), value);
        }

        public static string JwtToken
        {
            get => GetJwtToken(GetAppSettingFilename());
            set => SetJwtToken(GetAppSettingFilename(), value);
        }

        public static string LogLevel
        {
            get => GetLogLevel(GetAppSettingFilename());
            set => SetLogLevel(GetAppSettingFilename(), value);
        }

        public static string LogPath
        {
            get => GetLoggingFile(GetAppSettingFilename());
            set => SetLoggingFile(GetAppSettingFilename(), value);
        }

        public static string DatabasePath
        {
            get => GetDatabasePath(GetAppSettingFilename());
            set => SetDatabasePath(GetAppSettingFilename(), value);
        }

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
                var currentToken = GetJwtToken(filePath);
                var json = File.ReadAllText(filePath)
                    .Replace("\"TokenKey\": \"" + currentToken, "\"TokenKey\": \"" + token);
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
            if (new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker)
            {
                return;
            }

            try
            {
                var currentPort = GetPort(filePath);
                var json = File.ReadAllText(filePath).Replace("\"Port\": " + currentPort, "\"Port\": " + port);
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
            if (new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker)
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

        #region LogLevel

        private static void SetLogLevel(string filePath, string logLevel)
        {
            try
            {
                var currentLevel = GetLogLevel(filePath);
                var json = File.ReadAllText(filePath)
                    .Replace($"\"Default\": \"{currentLevel}\"", $"\"Default\": \"{logLevel}\"");
                File.WriteAllText(filePath, json);
            }
            catch (Exception)
            {
                /* Swallow Exception */
            }
        }

        private static string GetLogLevel(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var jsonObj = JsonSerializer.Deserialize<dynamic>(json);

                if (jsonObj.TryGetProperty("Logging", out JsonElement tokenElement))
                {
                    foreach (var property in tokenElement.EnumerateObject())
                    {
                        if (!property.Name.Equals("LogLevel")) continue;
                        foreach (var logProperty in property.Value.EnumerateObject())
                        {
                            if (logProperty.Name.Equals("Default"))
                            {
                                return logProperty.Value.GetString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing app settings: " + ex.Message);
            }

            return "Information";
        }

        #endregion

        private static string GetBranch(string filePath)
        {
            const string defaultBranch = "main";

            try
            {
                var json = File.ReadAllText(filePath);
                var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
                const string key = "Branch";

                if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
                {
                    return tokenElement.GetString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading app settings: " + ex.Message);
            }

            return defaultBranch;
        }

        private static void SetBranch(string filePath, string updatedBranch)
        {
            try
            {
                var currentBranch = GetBranch(filePath);
                var json = File.ReadAllText(filePath)
                    .Replace("\"Branch\": " + currentBranch, "\"Branch\": " + updatedBranch);
                File.WriteAllText(filePath, json);
            }
            catch (Exception)
            {
                /* Swallow Exception */
            }
        }

        private static string GetLoggingFile(string filePath)
        {
            const string defaultFile = "config/logs/kavita.log";

            try
            {
                var json = File.ReadAllText(filePath);
                var jsonObj = JsonSerializer.Deserialize<dynamic>(json);

                if (jsonObj.TryGetProperty("Logging", out JsonElement tokenElement))
                {
                    foreach (var property in tokenElement.EnumerateObject())
                    {
                        if (!property.Name.Equals("File")) continue;
                        foreach (var logProperty in property.Value.EnumerateObject())
                        {
                            if (logProperty.Name.Equals("Path"))
                            {
                                return logProperty.Value.GetString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing app settings: " + ex.Message);
            }

            return defaultFile;
        }

        /// <summary>
        /// This should NEVER be called except by <see cref="MigrateConfigFiles"/>
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="directory"></param>
        private static void SetLoggingFile(string filePath, string directory)
        {
            try
            {
                var currentFile = GetLoggingFile(filePath);
                var json = File.ReadAllText(filePath)
                    .Replace("\"Path\": \"" + currentFile + "\"", "\"Path\": \"" + directory + "\"");
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                /* Swallow Exception */
                Console.WriteLine(ex);
            }
        }

        private static string GetDatabasePath(string filePath)
        {
            const string defaultFile = "config/kavita.db";

            try
            {
                var json = File.ReadAllText(filePath);
                var jsonObj = JsonSerializer.Deserialize<dynamic>(json);

                if (jsonObj.TryGetProperty("ConnectionStrings", out JsonElement tokenElement))
                {
                    foreach (var property in tokenElement.EnumerateObject())
                    {
                        if (!property.Name.Equals("DefaultConnection")) continue;
                        return property.Value.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing app settings: " + ex.Message);
            }

            return defaultFile;
        }

        /// <summary>
        /// This should NEVER be called except by <see cref="MigrateConfigFiles"/>
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="updatedPath"></param>
        private static void SetDatabasePath(string filePath, string updatedPath)
        {
            try
            {
                var existingString = GetDatabasePath(filePath);
                var json = File.ReadAllText(filePath)
                    .Replace(existingString,
                        "Data source=" + updatedPath);
                File.WriteAllText(filePath, json);
            }
            catch (Exception)
            {
                /* Swallow Exception */
            }
        }
    }
}
