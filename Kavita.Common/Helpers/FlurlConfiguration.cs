using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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

    /// <summary>
    /// Downloads <paramref name="url"/> through <see cref="CreateSafeRequest"/> into <paramref name="filePath"/>.
    /// Throws <see cref="KavitaException"/> and leaves no file when the body is larger than <paramref name="maxBytes"/>
    /// </summary>
    public static async Task DownloadToFileAsync(string url, string filePath, long maxBytes, CancellationToken ct = default)
    {
        using var response = await CreateSafeRequest(url).GetAsync(HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.ResponseMessage.Content.Headers.ContentLength > maxBytes)
        {
            throw new KavitaException("download-over-size-limit");
        }

        try
        {
            await using var body = await response.GetStreamAsync();
            await using var file = File.Create(filePath);

            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await body.ReadAsync(buffer, ct)) > 0)
            {
                total += read;
                if (total > maxBytes)
                {
                    throw new KavitaException("download-over-size-limit");
                }

                await file.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        catch
        {
            File.Delete(filePath);
            throw;
        }
    }
}
