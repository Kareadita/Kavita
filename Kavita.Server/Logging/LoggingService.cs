using Kavita.API.Services;

namespace Kavita.Server.Logging;

public class LoggingService: ILoggingService
{
    public void SwitchLogLevel(string level)
    {
        LogLevelOptions.SwitchLogLevel(level);
    }
}
