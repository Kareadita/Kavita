using System.IO;
using API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace API.Logging;

/// <summary>
/// This class represents information for configuring Logging in the Application. Only a high log level is exposed and Kavita
/// controls the underlying log levels for different loggers in ASP.NET
/// </summary>
public static class LogLevelOptions
{
    public const string LogFile = "config/logs/kavita.log";
    public const bool LogRollingEnabled = true;
    /// <summary>
    /// Controls the Logging Level of the Application
    /// </summary>
    private static readonly LoggingLevelSwitch LogLevelSwitch = new ();
    /// <summary>
    /// Controls Microsoft's Logging Level
    /// </summary>
    private static readonly LoggingLevelSwitch MicrosoftLogLevelSwitch = new (LogEventLevel.Error);
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

    public static LoggerConfiguration CreateConfig(LoggerConfiguration configuration)
    {
        const string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {CorrelationId} {ThreadId}] [{Level}] {SourceContext} {Message:lj}{NewLine}{Exception}";
        return configuration
            .MinimumLevel
            .ControlledBy(LogLevelSwitch)
            .MinimumLevel.Override("Microsoft", MicrosoftLogLevelSwitch)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", MicrosoftHostingLifetimeLogLevelSwitch)
            .MinimumLevel.Override("Hangfire", HangfireLogLevelSwitch)
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Internal.WebHost", AspNetCoreLogLevelSwitch)
            // Suppress noisy loggers that add no value
            .MinimumLevel.Override("Microsoft.AspNetCore.ResponseCaching.ResponseCachingMiddleware", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Error)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Console(new MessageTemplateTextFormatter(outputTemplate))
            .WriteTo.File(LogFile,
                shared: true,
                rollingInterval: RollingInterval.Day,
                outputTemplate: outputTemplate);
    }

    public static void SwitchLogLevel(string level)
    {
        switch (level)
        {
            case "Debug":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Debug;
                MicrosoftLogLevelSwitch.MinimumLevel = LogEventLevel.Warning; // This is DB output information, Inf shows the SQL
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Information;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Warning;
                break;
            case "Information":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                MicrosoftLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                break;
            case "Trace":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Verbose;
                MicrosoftLogLevelSwitch.MinimumLevel = LogEventLevel.Information;
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Debug;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Information;
                break;
            case "Warning":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Warning;
                MicrosoftLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                break;
            case "Critical":
                LogLevelSwitch.MinimumLevel = LogEventLevel.Fatal;
                MicrosoftLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                MicrosoftHostingLifetimeLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                AspNetCoreLogLevelSwitch.MinimumLevel = LogEventLevel.Error;
                break;
        }
    }

}
