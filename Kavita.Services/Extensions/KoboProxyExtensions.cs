using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Services.Helpers.Kobo;
using Microsoft.AspNetCore.Http;

namespace Kavita.Services.Extensions;

public static class KoboProxyExtensions
{
    public static void CopyRequest(this HttpRequestMessage message, HttpRequest request, Func<string, bool> copyHeader, object? body = null)
    {
        foreach (var header in request.Headers)
        {
            if (copyHeader(header.Key))
            {
                message.Headers.TryAddWithoutValidation(header.Key, [.. header.Value]);
            }
        }

        if (body != null)
        {
            message.Content = JsonContent.Create(body, options: KoboJsonOptions.JsonSerializerOptions);
        }
        else if (request.ContentLength > 0 || request.Headers.ContainsKey("Transfer-Encoding"))
        {
            var content = new StreamContent(request.Body);
            foreach (var h in request.Headers.Where(h =>
                         h.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)))
            {
                content.Headers.TryAddWithoutValidation(h.Key, [.. h.Value]);
            }

            message.Content = content;
        }
    }

    public static async Task CopyResponse(this HttpResponse response, HttpResponseMessage message, Func<string, bool> copyHeader, CancellationToken cancellationToken)
    {
        response.StatusCode = (int)message.StatusCode;

        foreach (var header in message.Headers)
        {
            if (copyHeader(header.Key))
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        foreach (var header in message.Content.Headers)
        {
            response.Headers[header.Key] = header.Value.ToArray();
        }

        // Kestrel will do this for us
        response.Headers.Remove("transfer-encoding");

        await message.Content.CopyToAsync(response.Body, cancellationToken);
    }
}
