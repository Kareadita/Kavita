using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Metadata;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;
using API.Tests.Helpers;
using Hangfire;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class ScannerServiceTests: AbstractDbTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/ScanTests");

    public ScannerServiceTests(ITestOutputHelper testOutputHelper): base(testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        // Set up Hangfire to use in-memory storage for testing
        GlobalConfiguration.Configuration.UseInMemoryStorage();
    }


    protected async Task SetAllSeriesLastScannedInThePast(DataContext context, Library library, TimeSpan? duration = null)
    {
        foreach (var series in library.Series)
        {
            await SetLastScannedInThePast(context, series, duration, false);
        }
        await context.SaveChangesAsync();
    }

    protected async Task SetLastScannedInThePast(DataContext context, Series series, TimeSpan? duration = null, bool save = true)
    {
        duration ??= TimeSpan.FromMinutes(2);
        series.LastFolderScanned = DateTime.Now.Subtract(duration.Value);
        context.Series.Update(series);

        if (save)
        {
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ScanLibrary_ComicVine_PublisherFolder()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        var testcase = "Publisher - ComicVine.json";
        var library = await scannerHelper.GenerateScannerData(testcase);
        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Equal(4, postLib.Series.Count);
    }

    [Fact]
    public async Task ScanLibrary_ShouldCombineNestedFolder()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        var testcase = "Series and Series-Series Combined - Manga.json";
        var library = await scannerHelper.GenerateScannerData(testcase);
        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(2, postLib.Series.First().Volumes.Count);
    }


    [Fact]
    public async Task ScanLibrary_FlatSeries()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Flat Series - Manga.json";
        var library = await scannerHelper.GenerateScannerData(testcase);
        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(3, postLib.Series.First().Volumes.Count);

        // TODO: Trigger a deletion of ch 10
    }

    [Fact]
    public async Task ScanLibrary_FlatSeriesWithSpecialFolder()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Flat Series with Specials Folder Alt Naming - Manga.json";
        var library = await scannerHelper.GenerateScannerData(testcase);
        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(4, postLib.Series.First().Volumes.Count);
        Assert.NotNull(postLib.Series.First().Volumes.FirstOrDefault(v => v.Chapters.FirstOrDefault(c => c.IsSpecial) != null));
    }

    [Fact]
    public async Task ScanLibrary_FlatSeriesWithSpecialFolder_AlternativeNaming()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Flat Series with Specials Folder Alt Naming - Manga.json";
        var library = await scannerHelper.GenerateScannerData(testcase);
        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(4, postLib.Series.First().Volumes.Count);
        Assert.NotNull(postLib.Series.First().Volumes.FirstOrDefault(v => v.Chapters.FirstOrDefault(c => c.IsSpecial) != null));
    }

    [Fact]
    public async Task ScanLibrary_FlatSeriesWithSpecial()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Flat Special - Manga.json";

        var library = await scannerHelper.GenerateScannerData(testcase);
        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(3, postLib.Series.First().Volumes.Count);
        Assert.NotNull(postLib.Series.First().Volumes.FirstOrDefault(v => v.Chapters.FirstOrDefault(c => c.IsSpecial) != null));
    }

    [Fact]
    public async Task ScanLibrary_SeriesWithUnbalancedParenthesis()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Scan Library Parses as ( - Manga.json";

        var library = await scannerHelper.GenerateScannerData(testcase);
        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        var series = postLib.Series.First();

        Assert.Equal("Mika-nee no Tanryoku Shidou - Mika s Guide to Self-Confidence (THE IDOLM@STE", series.Name);
    }

    /// <summary>
    /// This is testing that if the first file is named A and has a localized name of B if all other files are named B, it should still group and name the series A
    /// </summary>
    [Fact]
    public async Task ScanLibrary_LocalizedSeries()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Series with Localized - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("My Dress-Up Darling v01.cbz", new ComicInfo()
        {
            Series = "My Dress-Up Darling",
            LocalizedSeries = "Sono Bisque Doll wa Koi wo Suru"
        });

        var library = await scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(3, postLib.Series.First().Volumes.Count);
    }

    [Fact]
    public async Task ScanLibrary_LocalizedSeries2()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Series with Localized 2 - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("Immoral Guild v01.cbz", new ComicInfo()
        {
            Series = "Immoral Guild",
            LocalizedSeries = "Futoku no Guild" // Filename has a capital N and localizedSeries has lowercase
        });

        var library = await scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var s = postLib.Series.First();
        Assert.Equal("Immoral Guild", s.Name);
        Assert.Equal("Futoku no Guild", s.LocalizedName);
        Assert.Equal(3, s.Volumes.Count);
    }


    /// <summary>
    /// Special Keywords shouldn't be removed from the series name and thus these 2 should group
    /// </summary>
    [Fact]
    public async Task ScanLibrary_ExtraShouldNotAffect()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Series with Extra - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("Vol.01.cbz", new ComicInfo()
        {
            Series = "The Novel's Extra",
        });

        var library = await scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var s = postLib.Series.First();
        Assert.Equal("The Novel's Extra", s.Name);
        Assert.Equal(2, s.Volumes.Count);
    }


    /// <summary>
    /// Files under a folder with a SP marker should group into one issue
    /// </summary>
    /// <remarks>https://github.com/Kareadita/Kavita/issues/3299</remarks>
    [Fact]
    public async Task ScanLibrary_ImageSeries_SpecialGrouping()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Image Series with SP Folder - Manga.json";

        var library = await scannerHelper.GenerateScannerData(testcase);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(3, postLib.Series.First().Volumes.Count);
    }

    /// <summary>
    /// This test is currently disabled because the Image parser is unable to support multiple files mapping into one single Special.
    /// https://github.com/Kareadita/Kavita/issues/3299
    /// </summary>
    public async Task ScanLibrary_ImageSeries_SpecialGrouping_NonEnglish()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Image Series with SP Folder (Non English) - Image.json";

        var library = await scannerHelper.GenerateScannerData(testcase);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var series = postLib.Series.First();
        Assert.Equal(3, series.Volumes.Count);
        var specialVolume = series.Volumes.FirstOrDefault(v => v.Name == Parser.SpecialVolume);
        Assert.NotNull(specialVolume);
        Assert.Single(specialVolume.Chapters);
        Assert.True(specialVolume.Chapters.First().IsSpecial);
        //Assert.Equal("葬送のフリーレン 公式ファンブック SP01", specialVolume.Chapters.First().Title);
    }


    [Fact]
    public async Task ScanLibrary_PublishersInheritFromChapters()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Flat Special - Manga.json";

        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("Uzaki-chan Wants to Hang Out! v01 (2019) (Digital) (danke-Empire).cbz", new ComicInfo()
        {
            Publisher = "Correct Publisher"
        });
        infos.Add("Uzaki-chan Wants to Hang Out! - 2022 New Years Special SP01.cbz", new ComicInfo()
        {
            Publisher = "Special Publisher"
        });
        infos.Add("Uzaki-chan Wants to Hang Out! - Ch. 103 - Kouhai and Control.cbz", new ComicInfo()
        {
            Publisher = "Chapter Publisher"
        });

        var library = await scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var publishers = postLib.Series.First().Metadata.People
            .Where(p => p.Role == PersonRole.Publisher);
        Assert.Equal(3, publishers.Count());
    }


    /// <summary>
    /// Tests that pdf parser handles the loose chapters correctly
    /// https://github.com/Kareadita/Kavita/issues/3148
    /// </summary>
    [Fact]
    public async Task ScanLibrary_LooseChapters_Pdf()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "PDF Comic Chapters - Comic.json";

        var library = await scannerHelper.GenerateScannerData(testcase);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var series = postLib.Series.First();
        Assert.Single(series.Volumes);
        Assert.Equal(4, series.Volumes.First().Chapters.Count);
    }

    [Fact]
    public async Task ScanLibrary_LooseChapters_Pdf_LN()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "PDF Comic Chapters - LightNovel.json";

        var library = await scannerHelper.GenerateScannerData(testcase);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var series = postLib.Series.First();
        Assert.Single(series.Volumes);
        Assert.Equal(4, series.Volumes.First().Chapters.Count);
    }

    /// <summary>
    /// This is the same as doing ScanFolder as the case where it can find the series is just ScanSeries
    /// </summary>
    [Fact]
    public async Task ScanSeries_NewChapterInNestedFolder()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Series with Localized - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("My Dress-Up Darling v01.cbz", new ComicInfo()
        {
            Series = "My Dress-Up Darling",
            LocalizedSeries = "Sono Bisque Doll wa Koi wo Suru"
        });

        var library = await scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        var series = postLib.Series.First();
        Assert.Equal(3, series.Volumes.Count);

        // Bootstrap a new file in the nested "Sono Bisque Doll wa Koi wo Suru" directory and perform a series scan
        var testDirectory = Path.Combine(_testDirectory, Path.GetFileNameWithoutExtension(testcase));
        await scannerHelper.Scaffold(testDirectory, ["My Dress-Up Darling/Sono Bisque Doll wa Koi wo Suru ch 11.cbz"]);

        // Now that a new file exists in the subdirectory, scan again
        await scanner.ScanSeries(series.Id);
        Assert.Single(postLib.Series);
        Assert.Equal(3, series.Volumes.Count);
        Assert.Equal(2, series.Volumes.First(v => v.MinNumber.Is(Parser.LooseLeafVolumeNumber)).Chapters.Count);
    }

    [Fact]
    public async Task ScanLibrary_LocalizedSeries_MatchesFilename()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Localized Name matches Filename - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("Futoku no Guild v01.cbz", new ComicInfo()
        {
            Series = "Immoral Guild",
            LocalizedSeries = "Futoku no Guild"
        });

        var library = await scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var s = postLib.Series.First();
        Assert.Equal("Immoral Guild", s.Name);
        Assert.Equal("Futoku no Guild", s.LocalizedName);
        Assert.Single(s.Volumes);
    }

    [Fact]
    public async Task ScanLibrary_LocalizedSeries_MatchesFilename_SameNames()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Localized Name matches Filename - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("Futoku no Guild v01.cbz", new ComicInfo()
        {
            Series = "Futoku no Guild",
            LocalizedSeries = "Futoku no Guild"
        });

        var library = await scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var s = postLib.Series.First();
        Assert.Equal("Futoku no Guild", s.Name);
        Assert.Equal("Futoku no Guild", s.LocalizedName);
        Assert.Single(s.Volumes);
    }

    [Fact]
    public async Task ScanLibrary_ExcludePattern_Works()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Exclude Pattern 1 - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);

        library.LibraryExcludePatterns = [new LibraryExcludePattern() {  Pattern = "**/Extra/*"  }];
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var s = postLib.Series.First();
        Assert.Equal(2, s.Volumes.Count);
    }

    [Fact]
    public async Task ScanLibrary_ExcludePattern_FlippedSlashes_Works()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Exclude Pattern 1 - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);

        library.LibraryExcludePatterns = [new LibraryExcludePattern() {  Pattern = "**\\Extra\\*"  }];
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var s = postLib.Series.First();
        Assert.Equal(2, s.Volumes.Count);
    }

    [Fact]
    public async Task ScanLibrary_MultipleRoots_MultipleScans_DataPersists_Forced()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Multiple Roots - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);

        var testDirectoryPath =
            Path.Join(
                Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/ScanTests"),
                testcase.Replace(".json", string.Empty));
        library.Folders =
        [
            new FolderPath() {Path = Path.Join(testDirectoryPath, "Root 1")},
            new FolderPath() {Path = Path.Join(testDirectoryPath, "Root 2")}
        ];

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Equal(2, postLib.Series.Count);
        var s = postLib.Series.First(s => s.Name == "Plush");
        Assert.Equal(2, s.Volumes.Count);
        var s2 = postLib.Series.First(s => s.Name == "Accel");
        Assert.Single(s2.Volumes);

        // Make a change (copy a file into only 1 root)
        var root1PlushFolder = Path.Join(testDirectoryPath, "Root 1/Antarctic Press/Plush");
        File.Copy(Path.Join(root1PlushFolder, "Plush v02.cbz"), Path.Join(root1PlushFolder, "Plush v03.cbz"));

        // Rescan to ensure nothing changes yet again
        await scanner.ScanLibrary(library.Id, true);

        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.Equal(2, postLib.Series.Count);
        s = postLib.Series.First(s => s.Name == "Plush");
        Assert.Equal(3, s.Volumes.Count);
        s2 = postLib.Series.First(s => s.Name == "Accel");
        Assert.Single(s2.Volumes);
    }

    /// <summary>
    /// Regression bug appeared where multi-root and one root gets a new file, on next scan of library,
    /// the series in the other root are deleted. (This is actually failing because the file in Root 1 isn't being detected)
    /// </summary>
    [Fact]
    public async Task ScanLibrary_MultipleRoots_MultipleScans_DataPersists_NonForced()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Multiple Roots - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);

        var testDirectoryPath =
            Path.Join(
                Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/ScanTests"),
                testcase.Replace(".json", string.Empty));
        library.Folders =
        [
            new FolderPath() {Path = Path.Join(testDirectoryPath, "Root 1")},
            new FolderPath() {Path = Path.Join(testDirectoryPath, "Root 2")}
        ];

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Equal(2, postLib.Series.Count);
        var s = postLib.Series.First(s => s.Name == "Plush");
        Assert.Equal(2, s.Volumes.Count);
        var s2 = postLib.Series.First(s => s.Name == "Accel");
        Assert.Single(s2.Volumes);

        // Make a change (copy a file into only 1 root)
        var root1PlushFolder = Path.Join(testDirectoryPath, "Root 1/Antarctic Press/Plush");
        File.Copy(Path.Join(root1PlushFolder, "Plush v02.cbz"), Path.Join(root1PlushFolder, "Plush v03.cbz"));

        // Emulate time passage by updating lastFolderScan to be a min in the past
        await SetLastScannedInThePast(context, s);

        // Rescan to ensure nothing changes yet again
        await scanner.ScanLibrary(library.Id, false);

        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Equal(2, postLib.Series.Count);
        s = postLib.Series.First(series => series.Name == "Plush");
        Assert.Equal(3, s.Volumes.Count);
        s2 = postLib.Series.First(series => series.Name == "Accel");
        Assert.Single(s2.Volumes);
    }

    [Fact]
    public async Task ScanLibrary_AlternatingRemoval_IssueReplication()
    {
        // https://github.com/Kareadita/Kavita/issues/3476#issuecomment-2661635558
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Alternating Removal - Manga.json";

        // Setup: Generate test library
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);

        var testDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(),
            "../../../Services/Test Data/ScannerService/ScanTests",
            testcase.Replace(".json", string.Empty));

        library.Folders =
        [
            new FolderPath() { Path = Path.Combine(testDirectoryPath, "Root 1") },
            new FolderPath() { Path = Path.Combine(testDirectoryPath, "Root 2") }
        ];

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var scanner = scannerHelper.CreateServices();

        // First Scan: Everything should be added
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Contains(postLib.Series, s => s.Name == "Accel");
        Assert.Contains(postLib.Series, s => s.Name == "Plush");

        // Second Scan: Remove Root 2, expect Accel to be removed
        library.Folders = [new FolderPath() { Path = Path.Combine(testDirectoryPath, "Root 1") }];
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        // Emulate time passage by updating lastFolderScan to be a min in the past
        foreach (var s in postLib.Series)
        {
            s.LastFolderScanned = DateTime.Now.Subtract(TimeSpan.FromMinutes(1));
            context.Series.Update(s);
        }
        await context.SaveChangesAsync();

        await scanner.ScanLibrary(library.Id);
        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.DoesNotContain(postLib.Series, s => s.Name == "Accel"); // Ensure Accel is gone
        Assert.Contains(postLib.Series, s => s.Name == "Plush");

        // Third Scan: Re-add Root 2, Accel should come back
        library.Folders =
        [
            new FolderPath() { Path = Path.Combine(testDirectoryPath, "Root 1") },
            new FolderPath() { Path = Path.Combine(testDirectoryPath, "Root 2") }
        ];
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        // Emulate time passage by updating lastFolderScan to be a min in the past
        foreach (var s in postLib.Series)
        {
            s.LastFolderScanned = DateTime.Now.Subtract(TimeSpan.FromMinutes(1));
            context.Series.Update(s);
        }
        await context.SaveChangesAsync();

        await scanner.ScanLibrary(library.Id);
        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Contains(postLib.Series, s => s.Name == "Accel"); // Accel should be back
        Assert.Contains(postLib.Series, s => s.Name == "Plush");

        // Emulate time passage by updating lastFolderScan to be a min in the past
        await SetAllSeriesLastScannedInThePast(context, postLib);

        // Fourth Scan: Run again to check stability (should not remove Accel)
        await scanner.ScanLibrary(library.Id);
        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Contains(postLib.Series, s => s.Name == "Accel");
        Assert.Contains(postLib.Series, s => s.Name == "Plush");
    }

    [Fact]
    public async Task ScanLibrary_DeleteSeriesInUI_ComeBack()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Delete Series In UI - Manga.json";

        // Setup: Generate test library
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);

        var testDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(),
            "../../../Services/Test Data/ScannerService/ScanTests",
            testcase.Replace(".json", string.Empty));

        library.Folders =
        [
            new FolderPath() { Path = Path.Combine(testDirectoryPath, "Root 1") },
            new FolderPath() { Path = Path.Combine(testDirectoryPath, "Root 2") }
        ];

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var scanner = scannerHelper.CreateServices();

        // First Scan: Everything should be added
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Contains(postLib.Series, s => s.Name == "Accel");
        Assert.Contains(postLib.Series, s => s.Name == "Plush");

        // Second Scan: Delete the Series
        library.Series = [];
        await unitOfWork.CommitAsync();

        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Empty(postLib.Series);

        await scanner.ScanLibrary(library.Id);
        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Contains(postLib.Series, s => s.Name == "Accel"); // Ensure Accel is gone
        Assert.Contains(postLib.Series, s => s.Name == "Plush");
    }

    [Fact]
    public async Task SubFolders_NoRemovals_ChangesFound()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Subfolders always scanning all series changes - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Equal(4, postLib.Series.Count);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(2, spiceAndWolf.Volumes.Count);
        Assert.Equal(3, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

        var frieren = postLib.Series.First(x => x.Name == "Frieren - Beyond Journey's End");
        Assert.Single(frieren.Volumes);
        Assert.Equal(2, frieren.Volumes.Sum(v => v.Chapters.Count));

        var executionerAndHerWayOfLife = postLib.Series.First(x => x.Name == "The Executioner and Her Way of Life");
        Assert.Equal(2, executionerAndHerWayOfLife.Volumes.Count);
        Assert.Equal(2, executionerAndHerWayOfLife.Volumes.Sum(v => v.Chapters.Count));

        await SetAllSeriesLastScannedInThePast(context, postLib);

        // Add a new chapter to a volume of the series, and scan. Validate that no chapters were lost, and the new
        // chapter was added
        var executionerCopyDir = Path.Join(Path.Join(testDirectoryPath, "The Executioner and Her Way of Life"),
            "The Executioner and Her Way of Life Vol. 1");
        File.Copy(Path.Join(executionerCopyDir, "The Executioner and Her Way of Life Vol. 1 Ch. 0001.cbz"),
            Path.Join(executionerCopyDir, "The Executioner and Her Way of Life Vol. 1 Ch. 0002.cbz"));

        await scanner.ScanLibrary(library.Id);
        await unitOfWork.CommitAsync();

        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Equal(4, postLib.Series.Count);

        spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(2, spiceAndWolf.Volumes.Count);
        Assert.Equal(3, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

        frieren = postLib.Series.First(x => x.Name == "Frieren - Beyond Journey's End");
        Assert.Single(frieren.Volumes);
        Assert.Equal(2, frieren.Volumes.Sum(v => v.Chapters.Count));

        executionerAndHerWayOfLife = postLib.Series.First(x => x.Name == "The Executioner and Her Way of Life");
        Assert.Equal(2, executionerAndHerWayOfLife.Volumes.Count);
        Assert.Equal(3, executionerAndHerWayOfLife.Volumes.Sum(v => v.Chapters.Count)); // Incremented by 1
    }

    [Fact]
    public async Task RemovalPickedUp_NoOtherChanges()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Series removed when no other changes are made - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Equal(2, postLib.Series.Count);

        var executionerCopyDir = Path.Join(testDirectoryPath, "The Executioner and Her Way of Life");
        Directory.Delete(executionerCopyDir, true);

        await scanner.ScanLibrary(library.Id);
        await unitOfWork.CommitAsync();

        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Single(postLib.Series, s => s.Name == "Spice and Wolf");
        Assert.Equal(2, postLib.Series.First().Volumes.Count);
    }

    [Fact]
    public async Task SubFoldersNoSubFolders_CorrectPickupAfterAdd()
    {
        // This test case is used in multiple tests and can result in conflict if not separated
        var (unitOfWork, context, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Subfolders and files at root (2) - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(3, spiceAndWolf.Volumes.Count);
        Assert.Equal(4, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

        await SetLastScannedInThePast(context, spiceAndWolf);

        // Add volume to Spice and Wolf series directory
        var spiceAndWolfDir = Path.Join(testDirectoryPath, "Spice and Wolf");
        File.Copy(Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 1.cbz"),
            Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 4.cbz"));

        await scanner.ScanLibrary(library.Id);

        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(4, spiceAndWolf.Volumes.Count);
        Assert.Equal(5, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

        await SetLastScannedInThePast(context, spiceAndWolf);

        // Add file in subfolder
        spiceAndWolfDir = Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 3");
        File.Copy(Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 3 Ch. 0012.cbz"),
            Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 3 Ch. 0013.cbz"));

        await scanner.ScanLibrary(library.Id);

        postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(4, spiceAndWolf.Volumes.Count);
        Assert.Equal(6, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

    }


    /// <summary>
    /// Ensure when Kavita scans, the sort order of chapters is correct
    /// </summary>
    [Fact]
    public async Task ScanLibrary_SortOrderWorks()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Sort Order - Manga.json";

        var library = await scannerHelper.GenerateScannerData(testcase);


        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);

        // Get the loose leaf volume and confirm each chapter aligns with expectation of Sort Order
        var series = postLib.Series.First();
        Assert.NotNull(series);

        var volume = series.Volumes.FirstOrDefault();
        Assert.NotNull(volume);

        var sortedChapters = volume.Chapters.OrderBy(c => c.SortOrder).ToList();
        Assert.True(sortedChapters[0].SortOrder.Is(1f));
        Assert.True(sortedChapters[1].SortOrder.Is(4f));
        Assert.True(sortedChapters[2].SortOrder.Is(5f));
    }


    [Fact]
    public async Task ScanLibrary_MetadataDisabled_NoOverrides()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Series with Localized No Metadata - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("Immoral Guild v01.cbz", new ComicInfo()
        {
            Series = "Immoral Guild",
            LocalizedSeries = "Futoku no Guild" // Filename has a capital N and localizedSeries has lowercase
        });

        var library = await scannerHelper.GenerateScannerData(testcase, infos);

        // Disable metadata
        library.EnableMetadata = false;
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        // Validate that there are 2 series
        Assert.NotNull(postLib);
        Assert.Equal(2, postLib.Series.Count);

        Assert.Contains(postLib.Series, x => x.Name == "Immoral Guild");
        Assert.Contains(postLib.Series, x => x.Name == "Futoku No Guild");
    }

    /// <summary>
    /// Test that if we have 3 directories with
    /// </summary>
    [Fact]
    public async Task ScanLibrary_MetadataDisabled_NoCombining()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Series with slight differences No Metadata - Manga.json";

        // Mark every series with metadata saying it belongs to Toradora
        var infos = new Dictionary<string, ComicInfo>
        {
            {"Toradora v01.cbz", new ComicInfo()
            {
                Series = "Toradora",
            }},
            {"Toradora! v01.cbz", new ComicInfo()
            {
                Series = "Toradora",
            }},
            {"Toradora! Light Novel v01.cbz", new ComicInfo()
            {
                Series = "Toradora",
            }},

        };

        var library = await scannerHelper.GenerateScannerData(testcase, infos);

        // Disable metadata
        library.EnableMetadata = false;
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        // Validate that there are 2 series
        Assert.NotNull(postLib);
        Assert.Equal(3, postLib.Series.Count);

        Assert.Contains(postLib.Series, x => x.Name == "Toradora");
        Assert.Contains(postLib.Series, x => x.Name == "Toradora!");
        Assert.Contains(postLib.Series, x => x.Name == "Toradora! Light Novel");
    }

    [Fact]
    public async Task ScanLibrary_SortName_NoPrefix()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, _testOutputHelper);

        const string testcase = "Series with Prefix - Book.json";

        var library = await scannerHelper.GenerateScannerData(testcase);

        library.RemovePrefixForSortName = true;
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var scanner = scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        Assert.Equal("The Avengers", postLib.Series.First().Name);
        Assert.Equal("Avengers", postLib.Series.First().SortName);
    }
}
