using System.IO;
using API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace API.Logging;

/// <summary>
/// This class represents information for configuring Logging in the Application. Only a high log level is exposed and Kavita
/// controls the underlying log levels for different loggers in ASP.NET
/// </summary>
public static class LogLevelOptions
{
    /// <summary>
    /// Controls the Logging Level of the Application
    /// </summary>
    private static readonly LoggingLevelSwitch LogLevelSwitch = new ();
    /// <summary>
    /// Controls Microsoft's Logging Level
    /// </summary>
    private static readonly LoggingLevelSwitch MicrosoftLogLevelSwitch = new (LogEventLevel.Information);
    /// <summary>
    /// Controls Microsoft.Hosting.Lifetime's Logging Level
    /// </summary>
    private static readonly LoggingLevelSwitch MicrosoftHostingLifetimeLogLevelSwitch = new (LogEventLevel.Error);
    /// <summary>
    /// Controls Hangfire's Logging Level
    /// </summary>
    private static readonly LoggingLevelSwitch HangfireLogLevelSwitch = new (LogEventLevel.Error);
    /// <summary>
    /// Controls Microsoft.AspNetCore.Hosting.Internal.WebHost's Logging Level
    /// </summary>
    private static readonly LoggingLevelSwitch AspNetCoreLogLevelSwitch = new (LogEventLevel.Error);

    // public static LoggerConfiguration Configuration = Configuration = new LoggerConfiguration()
    //     .MinimumLevel
    //     .ControlledBy(LogLevelSwitch)
    //     .MinimumLevel.Override("Microsoft", MicrosoftLogLevelSwitch)
    //     .MinimumLevel.Override("Microsoft.Hosting.Lifetime", MicrosoftHostingLifetimeLogLevelSwitch)
    //     .MinimumLevel.Override("Hangfire", HangfireLogLevelSwitch)
    //     .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Internal.WebHost", AspNetCoreLogLevelSwitch)
    //     .WriteTo.Console()
    //     .WriteTo.File(Path.Join("config/logs", "kavita.log"),
    //         shared: true,
    //         rollingInterval: RollingInterval.Day,
    //         outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {CorrelationId} {Level}] {Message:lj}{NewLine}{Exception}");


    public static LoggerConfiguration CreateConfig(LoggerConfiguration configuration)
    {
        return configuration
            .MinimumLevel
            .ControlledBy(LogLevelSwitch)
            .MinimumLevel.Override("Microsoft", MicrosoftLogLevelSwitch)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", MicrosoftHostingLifetimeLogLevelSwitch)
            .MinimumLevel.Override("Hangfire", HangfireLogLevelSwitch)
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Internal.WebHost", AspNetCoreLogLevelSwitch)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(Path.Join("config/logs", "kavita.log"),
                shared: true,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {CorrelationId} {Level}] {Message:lj}{NewLine}{Exception}");
    }

    public static void SwitchLogLevel(string level)
    {
        switch (level)
        {
            case "Debug":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Debug;
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Debug;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Debug;
                break;
            case "Information":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Information;
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                break;
            case "Verbose":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Verbose;
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Debug;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Debug;
                break;
            case "Warning":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Warning;
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                break;
            case "Fatal":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Fatal;
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                break;
            case "Error":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                break;
        }
    }

}
