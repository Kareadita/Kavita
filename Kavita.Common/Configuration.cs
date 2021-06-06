using System;
using System.IO;
using System.Text.Json;

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
    }
}