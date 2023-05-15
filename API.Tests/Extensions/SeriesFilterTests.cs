using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.Filtering.v2;
using API.Extensions.QueryExtensions.Filtering;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Tests.Extensions;

public class SeriesFilterTests : AbstractDbTest
{

    protected override Task ResetDb()
    {
        return Task.CompletedTask;
    }

    #region HasLanguage

    [Fact]
    public async Task HasLanguage_Works()
    {
        var foundSeries = await _context.Series.HasLanguage(true, FilterComparison.Contains, new List<string>() { }).ToListAsync();

    }

    #endregion
}
