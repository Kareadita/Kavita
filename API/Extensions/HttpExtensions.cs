using System.Linq;
using System.Text;
using System.Text.Json;
using API.Helpers;
using Microsoft.AspNetCore.Http;

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
            if (content == null || content.Length <= 0) return;
            using var sha1 = new System.Security.Cryptography.SHA256CryptoServiceProvider();
            response.Headers.Add("ETag", string.Concat(sha1.ComputeHash(content).Select(x => x.ToString("X2"))));
        }

        /// <summary>
        /// Calculates SHA256 hash for a cover image filename and sets as ETag. Ensures Cache-Control: private header is added.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="filename"></param>
        public static void AddCacheHeader(this HttpResponse response, string filename)
        {
            if (filename == null || filename.Length <= 0) return;
            using var sha1 = new System.Security.Cryptography.SHA256CryptoServiceProvider();
            response.Headers.Add("ETag", string.Concat(sha1.ComputeHash(Encoding.UTF8.GetBytes(filename)).Select(x => x.ToString("X2"))));
        }

    }
}
