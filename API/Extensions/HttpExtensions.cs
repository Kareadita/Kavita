using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using API.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace API.Extensions;
#nullable enable

public static class HttpExtensions
{
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
        var options = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        response.Headers.Append("Pagination", JsonSerializer.Serialize(paginationHeader, options));
        response.Headers.Append("Access-Control-Expose-Headers", "Pagination");
    }

    /// <summary>
    /// Calculates SHA256 hash for a byte[] and sets as ETag. Ensures Cache-Control: private header is added.
    /// </summary>
    /// <param name="response"></param>
    /// <param name="content">If byte[] is null or empty, will only add cache-control</param>
    public static void AddCacheHeader(this HttpResponse response, byte[] content)
    {
        if (content is not {Length: > 0}) return;
        response.Headers.Append(HeaderNames.ETag, string.Concat(SHA256.HashData(content).Select(x => x.ToString("X2"))));
        response.Headers.CacheControl =  $"private, max-age=100";
    }

}
