using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace API.Helpers;
#nullable enable

public class PagedList<T> : List<T>
{
    private PagedList(IEnumerable<T> items, int count, int pageNumber, int pageSize)
    {
        CurrentPage = pageNumber;
        TotalPages = (int) Math.Ceiling(count / (double) pageSize);
        PageSize = pageSize;
        TotalCount = count;
        AddRange(items);
    }

    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }

    public static async Task<PagedList<T>> CreateAsync(IQueryable<T> source, UserParams userParams)
    {
        return await CreateAsync(source, userParams.PageNumber, userParams.PageSize);
    }

    public static async Task<PagedList<T>> CreateAsync(IQueryable<T> source, int pageNumber, int pageSize)
    {
        // NOTE: OrderBy warning being thrown here even if query has the orderby statement
        var countTask = source.CountAsync();
        var itemsTask = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        await Task.WhenAll(countTask, itemsTask);

        return new PagedList<T>(itemsTask.Result, countTask.Result, pageNumber, pageSize);
    }
}
