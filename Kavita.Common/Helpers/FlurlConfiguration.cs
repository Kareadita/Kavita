using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using Flurl.Http;

namespace Kavita.Common.Helpers;

/// <summary>
/// Helper class for configuring Flurl client for a specific URL.
/// </summary>
public static class FlurlConfiguration
{
    private static readonly List<string> ConfiguredClients = new List<string>();
    private static readonly Dictionary<string, FlurlClient> SafeClients = new Dictionary<string, FlurlClient>();
    private static readonly Lock Lock = new Lock();

    /// <summary>
    /// Configures the Flurl client for the specified URL with SSL bypass only.
    /// Use this for trusted, hardcoded hosts (Kavita+ API, GitHub API, etc).
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
#pragma warning disable S4830
                cli.ServerCertificateCustomValidationCallback = (_, _, _, _) => true);
#pragma warning restore S4830

            ConfiguredClients.Add(host);
        }
    }

    /// <summary>
    /// Creates a Flurl request with SSRF protection for the given URL.
    /// Uses a SocketsHttpHandler with a ConnectCallback that validates every resolved IP
    /// against the blocklist at TCP connection time, preventing DNS rebinding and
    /// redirect-to-private-IP attacks. Use this for any URL derived from user input or external data.
    /// </summary>
    /// <param name="url">The URL to create a safe request for.</param>
    /// <returns>An <see cref="IFlurlRequest"/> configured with SSRF-safe connection handling.</returns>
    public static IFlurlRequest CreateSafeRequest(string url)
    {
        lock (Lock)
        {
            var ur = new Uri(url);
            var key = ur.Host + ":" + ur.Port;

            if (!SafeClients.TryGetValue(key, out var client))
            {
                var handler = new SocketsHttpHandler
                {
                    MaxAutomaticRedirections = 10,
                    ConnectCallback = async (context, ct) =>
                    {
                        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);

                        foreach (var addr in addresses)
                        {
                            if (IpBlocklist.IsBlockedAddress(addr)) continue;

                            var socket = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                            try
                            {
                                await socket.ConnectAsync(new IPEndPoint(addr, context.DnsEndPoint.Port), ct);
                                return new NetworkStream(socket, ownsSocket: true);
                            }
                            catch
                            {
                                socket.Dispose();
                                continue;
                            }
                        }


                        throw new KavitaException("url-blocked-address");
                    }
                };

                var httpClient = new HttpClient(handler);
                client = new FlurlClient(httpClient);
                SafeClients[key] = client;
            }

            return client.Request(url);
        }
    }
}
