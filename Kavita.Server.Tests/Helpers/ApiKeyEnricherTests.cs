using Kavita.Server.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Kavita.Server.Tests.Helpers;

public class ApiKeyEnricherTests
{
    private static LogEvent CreateEvent(string path)
    {
        var properties = new List<LogEventProperty>
        {
            new("SourceContext", new ScalarValue("Serilog.AspNetCore.RequestLoggingMiddleware")),
            new("RequestPath", new ScalarValue(path))
        };
        return new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, null,
            new MessageTemplate("test", Array.Empty<MessageTemplateToken>()), properties);
    }

    [Theory]
    [InlineData("/kobo/abc123/v1/library/sync", "/kobo/******REDACTED******/v1/library/sync")]
    [InlineData("/api/opds/abc123/", "/api/opds/******REDACTED******/")]
    [InlineData("/api/series?apiKey=abc&foo=1", "/api/series?apiKey=******REDACTED******&foo=1")]
    public void RedactsSensitivePaths(string input, string expected)
    {
        var logEvent = CreateEvent(input);
        var enricher = new ApiKeyEnricher();
        // Need a minimal ILogEventPropertyFactory here
        enricher.Enrich(logEvent, new SimplePropertyFactory());

        var actual = ((ScalarValue)logEvent.Properties["RequestPath"]).Value as string;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeavesCleanPathUnchanged()
    {
        var logEvent = CreateEvent("/api/series");
        new ApiKeyEnricher().Enrich(logEvent, new SimplePropertyFactory());
        Assert.Equal("/api/series",
            ((ScalarValue)logEvent.Properties["RequestPath"]).Value);
    }

    private class SimplePropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }
}
