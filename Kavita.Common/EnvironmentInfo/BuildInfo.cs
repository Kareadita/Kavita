using System;
using System.Linq;
using System.Reflection;

namespace Kavita.Common.EnvironmentInfo;

public static class BuildInfo
{
    public static readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
    static BuildInfo()
    {
        // var assembly = Assembly.GetExecutingAssembly();
        //
        // Version = assembly.GetName().Version;

        //var attributes = assembly.GetCustomAttributes(true);

        // Branch = "unknown";
        //
        // var config = attributes.OfType<AssemblyConfigurationAttribute>().FirstOrDefault();
        // if (config != null)
        // {
        //     Branch = config.Configuration; // NOTE: This is not helpful, better to have main/develop branch
        // }
        //
        // Release = $"{Version}-{Branch}";
    }

    public static string AppName { get; } = "Kavita";

    //public static Version Version { get; }
    public static string Branch { get; }
    public static string Release { get; }
}
