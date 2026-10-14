using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Tests.Helpers;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class ScannerServiceScanFolderTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    public static TheoryData<string, string, string?> Cases => new()
    {
        { Root + "Accel World", Root + "Accel World/Accel World v02.cbz", "Accel World" },
        { Root + "Accel World  Dural - Magisa Garden", Root + "Accel World  Dural - Magisa Garden/Accel World Dural v02.cbz", "Accel World Dural" },
        { Root + "March Story", Root + "March Story/March Story v01/March Story v01.cbz", "March Story" },
        { Root + "The Legend of Zelda", Root + "The Legend of Zelda/The Legend of Zelda - Twilight Princess v01.cbz", null },
        { Root + "YenPress", Root + "YenPress/New Series/New Series v01.cbz", null },
        { Root + "Spice and Wolf", Root + "Spice and Wolf/Spice and Wolf v03.cbz", "Spice and Wolf" },
        { Root + "March Story Extras", Root + "March Story Extras/March Story Extras v01.cbz", null },
        { Root + "Accel World", "", "Accel World" },
        { Root + "YenPress", "", null },
        { Root, "", null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task FindSeriesForFolder(string folder, string originalPath, string? expected)
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var library = new LibraryBuilder("Scan Folder", LibraryType.Manga)
            .WithFolderPath(new FolderPathBuilder(Root).Build())
            .WithSeries(SeriesInFolder("Accel World", Root + "Accel World", Root + "Accel World"))
            .WithSeries(SeriesInFolder("Accel World Dural", Root + "Accel World  Dural - Magisa Garden", Root + "Accel World  Dural - Magisa Garden"))
            .WithSeries(SeriesInFolder("March Story", Root + "March Story", Root + "March Story"))
            .WithSeries(SeriesInFolder("Twilight Princess", Root + "The Legend of Zelda", Root + "The Legend of Zelda"))
            .WithSeries(SeriesInFolder("Ocarina of Time", Root + "The Legend of Zelda", Root + "The Legend of Zelda"))
            .WithSeries(SeriesInFolder("Frieren", Root + "YenPress", Root + "YenPress/Frieren"))
            .WithSeries(SeriesInFolder("Spice and Wolf", Root + "Spice and Wolf", null))
            .Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var scanner = new ScannerHelper(unitOfWork, testOutputHelper).CreateServices();
        var series = await scanner.FindSeriesForFolder(folder, originalPath);

        Assert.Equal(expected, series?.Name);
    }

    private static Series SeriesInFolder(string name, string folderPath, string? lowestFolderPath)
    {
        var series = new SeriesBuilder(name).WithFormat(MangaFormat.Archive).Build();
        series.FolderPath = folderPath;
        series.LowestFolderPath = lowestFolderPath;
        return series;
    }
}
