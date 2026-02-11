using System;
using System.Collections.Generic;

namespace Kavita.Common.Helpers;

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

    public static PagedList<T> Create(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize)
    {
        return new PagedList<T>(items, totalCount, pageNumber, pageSize);
    }
}
