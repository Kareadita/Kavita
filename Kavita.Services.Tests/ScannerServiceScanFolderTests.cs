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
        { "M:/Accel World", "M:/Accel World/Accel World v02.cbz", "Accel World" },
        { "M:/Accel World  Dural - Magisa Garden", "M:/Accel World  Dural - Magisa Garden/Accel World Dural v02.cbz", "Accel World Dural" },
        { "M:/March Story", "M:/March Story/March Story v01/March Story v01.cbz", "March Story" },
        { "M:/The Legend of Zelda", "M:/The Legend of Zelda/The Legend of Zelda - Twilight Princess v01.cbz", null },
        { "M:/YenPress", "M:/YenPress/New Series/New Series v01.cbz", null },
        { "M:/Spice and Wolf", "M:/Spice and Wolf/Spice and Wolf v03.cbz", "Spice and Wolf" },
        { "M:/March Story Extras", "M:/March Story Extras/March Story Extras v01.cbz", null },
        { "M:/Accel World", "", "Accel World" },
        { "M:/YenPress", "", null },
        { "M:/", "", null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task FindSeriesForFolder(string folder, string originalPath, string? expected)
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var library = new LibraryBuilder("Scan Folder", LibraryType.Manga)
            .WithFolderPath(new FolderPathBuilder("M:/").Build())
            .WithSeries(SeriesInFolder("Accel World", "M:/Accel World", "M:/Accel World"))
            .WithSeries(SeriesInFolder("Accel World Dural", "M:/Accel World  Dural - Magisa Garden", "M:/Accel World  Dural - Magisa Garden"))
            .WithSeries(SeriesInFolder("March Story", "M:/March Story", "M:/March Story"))
            .WithSeries(SeriesInFolder("Twilight Princess", "M:/The Legend of Zelda", "M:/The Legend of Zelda"))
            .WithSeries(SeriesInFolder("Ocarina of Time", "M:/The Legend of Zelda", "M:/The Legend of Zelda"))
            .WithSeries(SeriesInFolder("Frieren", "M:/YenPress", "M:/YenPress/Frieren"))
            .WithSeries(SeriesInFolder("Spice and Wolf", "M:/Spice and Wolf", null))
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
