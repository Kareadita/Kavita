using System;
using System.Collections.Generic;
using System.Threading;
using Flurl.Http;

namespace Kavita.Common.Helpers;

/// <summary>
/// Helper class for configuring Flurl client for a specific URL.
/// </summary>
public static class FlurlConfiguration
{
    private static readonly List<string> ConfiguredClients = new List<string>();
    private static readonly Lock Lock = new Lock();

    /// <summary>
    /// Configures the Flurl client for the specified URL.
    /// </summary>
    /// <param name="url">The URL to configure the client for.</param>
    public static void ConfigureClientForUrl(string url)
    {
        //Important client are mapped without path, per example two urls pointing to the same host:port but different path, will use the same client.
        lock (Lock)
        {
            var ur = new Uri(url);
            //key is host:port
            var host = ur.Host + ":" + ur.Port;
            if (ConfiguredClients.Contains(host)) return;

            FlurlHttp.ConfigureClientForUrl(url).ConfigureInnerHandler(cli =>
                cli.ServerCertificateCustomValidationCallback = (_, _, _, _) => true);

            ConfiguredClients.Add(host);
        }
    }
}
