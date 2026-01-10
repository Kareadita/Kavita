using System.Text.Json;
using API.Helpers;
using Microsoft.AspNetCore.Http;

namespace API.Extensions;
#nullable enable

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
}
