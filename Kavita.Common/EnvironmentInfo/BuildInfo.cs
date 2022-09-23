using System;
using System.Reflection;

namespace Kavita.Common.EnvironmentInfo;

public static class BuildInfo
{
    public static readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
    public static string AppName { get; } = "Kavita";

}
