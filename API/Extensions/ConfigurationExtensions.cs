using Microsoft.Extensions.Configuration;

namespace API.Extensions;

public static class ConfigurationExtensions
{
    public static int GetMaxRollingFiles(this IConfiguration config)
    {
        return int.Parse(config.GetSection("Logging").GetSection("File").GetSection("MaxRollingFiles").Value);
    }
    public static string GetLoggingFileName(this IConfiguration config)
    {
        return config.GetSection("Logging").GetSection("File").GetSection("Path").Value;
    }
}
