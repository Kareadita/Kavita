using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Extensions;

public static class PagedListExtensions
{
    extension<T>(PagedList<T> pagedList)
    {
        public static async Task<PagedList<T>> CreateAsync(IQueryable<T> source, UserParams userParams, CancellationToken ct = default)
        {
            return await PagedList<T>.CreateAsync(source, userParams.PageNumber, userParams.PageSize, ct);
        }

        public static async Task<PagedList<T>> CreateAsync(IQueryable<T> source, int pageNumber, int pageSize, CancellationToken ct = default)
        {
            // NOTE: OrderBy warning being thrown here even if query has the orderby statement
            var countTask = source.CountAsync(ct);
            var itemsTask = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            await Task.WhenAll(countTask, itemsTask);

            return PagedList<T>.Create(itemsTask.Result, countTask.Result, pageNumber, pageSize);
        }
    }
}
