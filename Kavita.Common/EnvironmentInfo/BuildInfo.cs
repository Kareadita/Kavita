using System;
using System.Reflection;

namespace Kavita.Common.EnvironmentInfo;

public static class BuildInfo
{
    public static readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
    public static string AppName { get; } = "Kavita";
    /// <summary>
    /// New on every process start. Lets clients tell that the server restarted
    /// </summary>
    public static readonly Guid BootId = Guid.NewGuid();
    public static readonly DateTime StartedUtc = DateTime.UtcNow;

}
