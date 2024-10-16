using System;
using System.Collections.Generic;
using System.Net.Http;
using Flurl.Http;


namespace Kavita.Common.Helpers;

/// <summary>
/// Helper class for configuring Flurl client for a specific URL.
/// </summary>
public static class FlurlConfiguration
{
    private static readonly List<string> _configuredClients = new List<string>();
    private static readonly object _lock = new object();

    /// <summary>
    /// Configures the Flurl client for the specified URL.
    /// </summary>
    /// <param name="url">The URL to configure the client for.</param>
    public static void ConfigureClientForUrl(string url)
    {
        //Important client are mapped without path, per example two urls pointing to the same host:port but different path, will use the same client.
        lock (_lock)
        {
            Uri ur = new Uri(url);
            //key is host:port
            string host = ur.Host + ":" + ur.Port;
            if (_configuredClients.Contains(host))
                return;
            FlurlHttp.ConfigureClientForUrl(url).ConfigureInnerHandler(cli =>
                cli.ServerCertificateCustomValidationCallback = (_, _, _, _) => true);
            _configuredClients.Add(host);
        }
    }
}
