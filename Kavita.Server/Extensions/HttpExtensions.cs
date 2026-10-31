using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Http;

namespace Kavita.Server.Extensions;

public static class HttpExtensions
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Adds pagination headers - Use with <see cref="PagedList{T}"/>
    /// </summary>
    /// <param name="response"></param>
    /// <param name="currentPage"></param>
    /// <param name="itemsPerPage"></param>
    /// <param name="totalItems"></param>
    /// <param name="totalPages"></param>
    public static void AddPaginationHeader(this HttpResponse response, int currentPage,
        int itemsPerPage, int totalItems, int totalPages)
    {
        var paginationHeader = new PaginationHeader(currentPage, itemsPerPage, totalItems, totalPages);

        response.Headers.Append("Pagination", JsonSerializer.Serialize(paginationHeader, Options));
        response.Headers.Append("Access-Control-Expose-Headers", "Pagination");
    }

    public static void AddPaginationHeader<T>(this HttpResponse response, PagedList<T> pagedList)
    {
        response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);
    }

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
            message.Content = JsonContent.Create(body, options: KoboJsonAttribute.JsonSerializerOptions);
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
