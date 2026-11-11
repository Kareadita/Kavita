using System;
using Microsoft.IdentityModel.Protocols;
using Serilog;

namespace Kavita.Server.Extensions;

public static class ConfigurationManagerExtensions
{
    public static T? GetSync<T>(this ConfigurationManager<T> configurationManager)
        where T : class
    {
        try
        {
            return configurationManager.GetConfigurationAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "An error occured loading {Class} configuration", typeof(T).Name);
        }

        return null;
    }
}
