using System.Text.RegularExpressions;
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
        const string outputTemplate = "[Kavita] [{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {CorrelationId} {ThreadId}] [{Level}] {SourceContext} {Message:lj}{NewLine}{Exception}";
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
            .Enrich.With(new ApiKeyEnricher())
            .WriteTo.Console(new MessageTemplateTextFormatter(outputTemplate))
            .WriteTo.File(LogFile,
                shared: true,
                rollingInterval: RollingInterval.Day,
                outputTemplate: outputTemplate)
            .Filter.ByIncludingOnly(ShouldIncludeLogStatement);
    }

    private static bool ShouldIncludeLogStatement(LogEvent e)
    {
        var isRequestLoggingMiddleware = e.Properties.ContainsKey("SourceContext") &&
                                         e.Properties["SourceContext"].ToString().Replace("\"", string.Empty) ==
                                         "Serilog.AspNetCore.RequestLoggingMiddleware";

        // If Minimum log level is Warning, swallow all Request Logging messages
        if (isRequestLoggingMiddleware && LogLevelSwitch.MinimumLevel > LogEventLevel.Information)
        {
            return false;
        }

        if (isRequestLoggingMiddleware)
        {
            var path = e.Properties["Path"].ToString().Replace("\"", string.Empty);
            if (e.Properties.ContainsKey("Path") && path == "/api/health") return false;
            if (e.Properties.ContainsKey("Path") && path == "/hubs/messages") return false;
            if (e.Properties.ContainsKey("Path") && path.StartsWith("/api/image")) return false;
        }

        return true;
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
                LogLevelSwitch.MinimumLevel = LogEventLevel.Information;
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

public partial class ApiKeyEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent e, ILogEventPropertyFactory propertyFactory)
    {
        var isRequestLoggingMiddleware = e.Properties.ContainsKey("SourceContext") &&
                                         e.Properties["SourceContext"].ToString().Replace("\"", string.Empty) ==
                                         "Serilog.AspNetCore.RequestLoggingMiddleware";
        if (!isRequestLoggingMiddleware) return;
        if (!e.Properties.ContainsKey("RequestPath") ||
            !e.Properties["RequestPath"].ToString().Contains("apiKey=")) return;

        // Check if the log message contains "apiKey=" and censor it
        var censoredMessage = MyRegex().Replace(e.Properties["RequestPath"].ToString(), "apiKey=******REDACTED******");
        var enrichedProperty = propertyFactory.CreateProperty("RequestPath", censoredMessage);
        e.AddOrUpdateProperty(enrichedProperty);
    }

    [GeneratedRegex(@"\bapiKey=[^&\s]+\b")]
    private static partial Regex MyRegex();
}
