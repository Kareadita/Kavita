using System;
using System.Collections.Generic;
using System.Net.Http;
using Flurl.Http;


namespace Kavita.Common.Helpers;

public static class FlurlConfiguration
{
    private static readonly List<string> _configuredClients = new List<string>();
    private static readonly object _lock = new object();

    public static void ConfigureClientForUrl(string url)
    {
        lock (_lock)
        {
            Uri ur = new Uri(url);
            string host = ur.Host+":"+ur.Port;
            if (_configuredClients.Contains(host))
            {
                return;
            }
            FlurlHttp.ConfigureClientForUrl(url).ConfigureInnerHandler(cli =>
                cli.ServerCertificateCustomValidationCallback = (_, _, _, _) => true);
            _configuredClients.Add(host);
        }
    }

}
