﻿using System;
using System.IO;
using System.Text.Json;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Hosting;

namespace Kavita.Common
{
   public static class Configuration
   {
      private static readonly string AppSettingsFilename = GetAppSettingFilename();
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

      private static string GetAppSettingFilename()
      {
         if (!string.IsNullOrEmpty(AppSettingsFilename))
         {
            return AppSettingsFilename;
         }
         var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
         var isDevelopment = environment == Environments.Development;
         return "appsettings" + (isDevelopment ? ".Development" : "") + ".json";
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
   }
}
