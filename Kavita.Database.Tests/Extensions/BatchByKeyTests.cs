using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Database.Extensions;
using Kavita.Models.Builders;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Database.Tests.Extensions;

public class BatchByKeyTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    [Fact]
    public async Task UnchangedRows_EachVisitedOnce()
    {
        var context = await SeedSeries(25);

        var ids = new List<int>();
        await foreach (var batch in context.Series.Select(s => s.Id).BatchByKeyAsync(id => id, 10))
        {
            ids.AddRange(batch);
        }

        Assert.Equal(25, ids.Count);
        Assert.Equal(25, ids.Distinct().Count());
    }

    [Fact]
    public async Task RowsLeavingTheFilter_EveryRowStillVisited()
    {
        var context = await SeedSeries(25);

        var visited = 0;
        var query = context.Series.Where(s => s.LowestFolderPath != null);
        await foreach (var batch in query.BatchByKeyAsync(s => s.Id, 10))
        {
            visited += batch.Count;
            foreach (var series in batch)
            {
                series.LowestFolderPath = null;
            }
            await context.SaveChangesAsync();
        }

        Assert.Equal(25, visited);
        Assert.Equal(0, await context.Series.CountAsync(s => s.LowestFolderPath != null));
    }

    private async Task<DataContext> SeedSeries(int count)
    {
        var (_, context, _) = await CreateDatabase();

        var library = new LibraryBuilder("lib0").Build();
        for (var i = 0; i < count; i++)
        {
            var series = new SeriesBuilder($"series{i}").Build();
            series.LowestFolderPath = $"/manga/series{i}";
            library.Series.Add(series);
        }
        context.Library.Add(library);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return context;
    }
}
