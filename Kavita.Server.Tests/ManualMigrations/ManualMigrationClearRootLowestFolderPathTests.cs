using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Server.ManualMigrations.v0._9._2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Server.Tests.ManualMigrations;

public class ManualMigrationClearRootLowestFolderPathTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private static Series SeriesWithLowestFolder(string name, string? lowestFolderPath)
    {
        var series = new SeriesBuilder(name).WithFormat(MangaFormat.Archive).Build();
        series.FolderPath = "M:/" + name;
        series.LowestFolderPath = lowestFolderPath;
        return series;
    }

    [Fact]
    public async Task ClearsOnlyValuesNotInsideTheLibrary()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var library = new LibraryBuilder("Manga")
            .WithFolderPath(new FolderPathBuilder(@"M:\").Build())
            .WithSeries(SeriesWithLowestFolder("Drive", "M:"))
            .WithSeries(SeriesWithLowestFolder("Drive Slash", "M:/"))
            .WithSeries(SeriesWithLowestFolder("Other Drive", "N:/Higurashi"))
            .WithSeries(SeriesWithLowestFolder("Valid", "M:/Higurashi When They Cry"))
            .WithSeries(SeriesWithLowestFolder("Nested", "M:/Higurashi When They Cry/Arc 1"))
            .WithSeries(SeriesWithLowestFolder("Never Set", null))
            .Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        await new ManualMigrationClearRootLowestFolderPath().RunAsync(context, Substitute.For<ILogger<Program>>());

        var lowestFolders = await context.Series
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Name, s => s.LowestFolderPath);

        Assert.Null(lowestFolders["Drive"]);
        Assert.Null(lowestFolders["Drive Slash"]);
        Assert.Null(lowestFolders["Other Drive"]);
        Assert.Equal("M:/Higurashi When They Cry", lowestFolders["Valid"]);
        Assert.Equal("M:/Higurashi When They Cry/Arc 1", lowestFolders["Nested"]);
        Assert.Null(lowestFolders["Never Set"]);
    }
}
