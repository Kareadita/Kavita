﻿using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using API.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace API.Extensions
{
    public static class HttpExtensions
    {
        public static void AddPaginationHeader(this HttpResponse response, int currentPage,
            int itemsPerPage, int totalItems, int totalPages)
        {
            var paginationHeader = new PaginationHeader(currentPage, itemsPerPage, totalItems, totalPages);
            var options = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            response.Headers.Add("Pagination", JsonSerializer.Serialize(paginationHeader, options));
            response.Headers.Add("Access-Control-Expose-Headers", "Pagination");
        }

        /// <summary>
        /// Calculates SHA256 hash for a byte[] and sets as ETag. Ensures Cache-Control: private header is added.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="content">If byte[] is null or empty, will only add cache-control</param>
        public static void AddCacheHeader(this HttpResponse response, byte[] content)
        {
            if (content is not {Length: > 0}) return;
            using var sha1 = SHA256.Create();

            response.Headers.Add(HeaderNames.ETag, string.Concat(sha1.ComputeHash(content).Select(x => x.ToString("X2"))));
        }

        /// <summary>
        /// Calculates SHA256 hash for a cover image filename and sets as ETag. Ensures Cache-Control: private header is added.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="filename"></param>
        /// <param name="maxAge">Maximum amount of seconds to set for Cache-Control. Defaults to 10 seconds</param>
        public static void AddCacheHeader(this HttpResponse response, string filename, int maxAge = 10)
        {
            if (filename is not {Length: > 0}) return;
            var hashContent = filename + File.GetLastWriteTimeUtc(filename);
            using var sha1 = SHA256.Create();
            response.Headers.Add(HeaderNames.ETag, string.Concat(sha1.ComputeHash(Encoding.UTF8.GetBytes(hashContent)).Select(x => x.ToString("X2"))));

            response.Headers.CacheControl =  $"public,max-age={maxAge}";
        }

    }
}
