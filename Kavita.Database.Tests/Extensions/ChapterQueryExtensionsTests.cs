using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Database.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Builders;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Database.Tests.Extensions;

public class ChapterQueryExtensionsTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    [Fact]
    public async Task GetSeriesPositionsAsync_MatchesApplyDefaultChapterOrdering()
    {
        var (_, ctx, _) = await CreateDatabase();
        var library = await ctx.Library.FirstAsync();

        // One series mixing regular volumes, a loose-leaf volume and a special volume,
        var series = new SeriesBuilder("Mixed Series")
            .WithLibraryId(library.Id)
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("1").WithSortOrder(1).Build())
                .WithChapter(new ChapterBuilder("2").WithSortOrder(2).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithSortOrder(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(ParserConstants.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").WithSortOrder(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(ParserConstants.SpecialVolume)
                .WithChapter(new ChapterBuilder("1").WithIsSpecial(true).WithSortOrder(1).Build())
                .WithChapter(new ChapterBuilder("2").WithSortOrder(2).Build())
                .Build())
            .Build();
        var otherSeries = new SeriesBuilder("Other Series")
            .WithLibraryId(library.Id)
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithSortOrder(1).Build())
                .WithChapter(new ChapterBuilder("2").WithSortOrder(2).Build())
                .Build())
            .Build();

        ctx.Series.Add(series);
        ctx.Series.Add(otherSeries);
        await ctx.SaveChangesAsync();

        var allChapterIds = await ctx.Chapter.Select(c => c.Id).ToListAsync();

        var expected = new Dictionary<int, int>();
        var perSeries = new Dictionary<int, int>();
        var ordered = await ctx.Chapter
            .ApplyDefaultChapterOrdering()
            .Select(c => new { c.Id, c.Volume.SeriesId })
            .ToListAsync();
        foreach (var c in ordered)
        {
            perSeries[c.SeriesId] = perSeries.GetValueOrDefault(c.SeriesId) + 1;
            expected[c.Id] = perSeries[c.SeriesId];
        }

        var actual = await ctx.Chapter.GetSeriesPositionsAsync(allChapterIds);

        Assert.Equal(expected, actual);

        var vol1Chapter = series.Volumes.Single(v => v.MinNumber == 1f).Chapters.Single();
        var vol2Chapters = series.Volumes.Single(v => v.MinNumber == 2f).Chapters.OrderBy(c => c.SortOrder).ToList();
        var looseLeaf = series.Volumes.Single(v => v.MinNumber == ParserConstants.LooseLeafVolumeNumber).Chapters.Single();
        var specialChapters = series.Volumes.Single(v => v.MinNumber == ParserConstants.SpecialVolumeNumber).Chapters.OrderBy(c => c.SortOrder).ToList();

        Assert.Equal(1, actual[vol1Chapter.Id]);
        Assert.Equal(2, actual[vol2Chapters[0].Id]);
        Assert.Equal(3, actual[vol2Chapters[1].Id]);
        Assert.Equal(4, actual[looseLeaf.Id]);
        // Within the special volume the non-special chapter (P3 = 0) sorts before the special one (P3 = 1)
        Assert.Equal(6, actual[specialChapters[0].Id]);
        Assert.Equal(5, actual[specialChapters[1].Id]);

        // Seperate ordering
        Assert.Equal(1, actual[otherSeries.Volumes.Single().Chapters.OrderBy(c => c.SortOrder).First().Id]);
        Assert.Equal(2, actual[otherSeries.Volumes.Single().Chapters.OrderBy(c => c.SortOrder).Last().Id]);
    }
}
