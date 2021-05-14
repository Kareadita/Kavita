﻿using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace Kavita.Common
{
    public class Configuration
    {
        private static string GetAppSettingFilename()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isDevelopment = environment == Environments.Development;
            return "appSettings" + (isDevelopment ? ".Development" : "") + ".json";
        }
        
        public static bool CheckIfJWTTokenSet()
        {
            try {
                var filePath = Path.Combine(AppContext.BaseDirectory, GetAppSettingFilename());
                var json = File.ReadAllText(filePath);
                var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
                const string key = "TokenKey";
                

                if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
                {
                    return tokenElement.GetString() != "super secret unguessable key";
                }

                return false;
                
            }
            catch (Exception ex) {
                Console.WriteLine("Error writing app settings: " + ex.Message);
            }

            return false;
        }

        public static bool UpdateJWTToken(string token)
        {
            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, GetAppSettingFilename());
                string json = File.ReadAllText(filePath);

                json = json.Replace("super secret unguessable key", token);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}