using System;
using System.IO;
using System.Text.Json;
using Kavita.Common.EnvironmentInfo;

namespace Kavita.Common
{
    public static class Configuration
    {

        public static bool CheckIfJwtTokenSet(string filePath)
        {
            try {
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
        

        public static bool UpdateJwtToken(string filePath, string token)
        {
            try
            {
                var json = File.ReadAllText(filePath).Replace("super secret unguessable key", token);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool UpdatePort(string filePath, int port)
        {
            if (new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker)
            {
                return true;
            }
            
            try
            {
                var currentPort = GetPort(filePath);
                var json = File.ReadAllText(filePath).Replace("\"Port\": " + currentPort, "\"Port\": " + port);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        public static int GetPort(string filePath)
        {
            const int defaultPort = 5000;
            if (new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker)
            {
                return defaultPort;
            }
            
            try {
                var json = File.ReadAllText(filePath);
                var jsonObj = JsonSerializer.Deserialize<dynamic>(json);
                const string key = "Port";
                
                if (jsonObj.TryGetProperty(key, out JsonElement tokenElement))
                {
                    return tokenElement.GetInt32();
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Error writing app settings: " + ex.Message);
            }

            return defaultPort;
        }
    }
}