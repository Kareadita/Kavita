using System.Linq;
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
        /// Calculates SHA1 hash for a byte[] and sets as ETag. Ensures Cache-Control: private header is added.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="content">If byte[] is null or empty, will only add cache-control</param>
        public static void AddCacheHeader(this HttpResponse response, byte[] content)
        {
            // Calculates SHA1 Hash for byte[]
            if (content == null || content.Length <= 0) return;
            using var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider();
            response.Headers.Add("ETag", string.Concat(sha1.ComputeHash(content).Select(x => x.ToString("X2"))));

            // Not Needed with Response Caching
            // if (!response.Headers.Keys.Contains("Cache-Control"))
            // {
            //     response.Headers.Add("Cache-Control", "private");    
            // }

        }
        
    }
}