using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using API.Data;
using API.Data.Metadata;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.Services.Tasks;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using API.Tests.Helpers;
using Hangfire;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class ScannerServiceTests : AbstractDbTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ScannerHelper _scannerHelper;
    private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/ScanTests");

    public ScannerServiceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        // Set up Hangfire to use in-memory storage for testing
        GlobalConfiguration.Configuration.UseInMemoryStorage();
        _scannerHelper = new ScannerHelper(_unitOfWork, testOutputHelper);
    }

    protected override async Task ResetDb()
    {
        _context.Library.RemoveRange(_context.Library);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task ScanLibrary_ComicVine_PublisherFolder()
    {
        var testcase = "Publisher - ComicVine.json";
        var library = await _scannerHelper.GenerateScannerData(testcase);
        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Equal(4, postLib.Series.Count);
    }

    [Fact]
    public async Task ScanLibrary_ShouldCombineNestedFolder()
    {
        var testcase = "Series and Series-Series Combined - Manga.json";
        var library = await _scannerHelper.GenerateScannerData(testcase);
        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(2, postLib.Series.First().Volumes.Count);
    }


    [Fact]
    public async Task ScanLibrary_FlatSeries()
    {
        var testcase = "Flat Series - Manga.json";
        var library = await _scannerHelper.GenerateScannerData(testcase);
        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(3, postLib.Series.First().Volumes.Count);

        // TODO: Trigger a deletion of ch 10
    }

    [Fact]
    public async Task ScanLibrary_FlatSeriesWithSpecialFolder()
    {
        var testcase = "Flat Series with Specials Folder - Manga.json";
        var library = await _scannerHelper.GenerateScannerData(testcase);
        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(4, postLib.Series.First().Volumes.Count);
        Assert.NotNull(postLib.Series.First().Volumes.FirstOrDefault(v => v.Chapters.FirstOrDefault(c => c.IsSpecial) != null));
    }

    [Fact]
    public async Task ScanLibrary_FlatSeriesWithSpecial()
    {
        const string testcase = "Flat Special - Manga.json";

        var library = await _scannerHelper.GenerateScannerData(testcase);
        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(3, postLib.Series.First().Volumes.Count);
        Assert.NotNull(postLib.Series.First().Volumes.FirstOrDefault(v => v.Chapters.FirstOrDefault(c => c.IsSpecial) != null));
    }


    [Fact]
    public async Task ScanLibrary_SeriesWithUnbalancedParenthesis()
    {
        const string testcase = "Scan Library Parses as ( - Manga.json";

        var library = await _scannerHelper.GenerateScannerData(testcase);
        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

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
        const string testcase = "Series with Localized - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("My Dress-Up Darling v01.cbz", new ComicInfo()
        {
            Series = "My Dress-Up Darling",
            LocalizedSeries = "Sono Bisque Doll wa Koi wo Suru"
        });

        var library = await _scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        Assert.Equal(3, postLib.Series.First().Volumes.Count);
    }

    [Fact]
    public async Task ScanLibrary_LocalizedSeries2()
    {
        const string testcase = "Series with Localized 2 - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("Immoral Guild v01.cbz", new ComicInfo()
        {
            Series = "Immoral Guild",
            LocalizedSeries = "Futoku no Guild" // Filename has a capital N and localizedSeries has lowercase
        });

        var library = await _scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

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
        const string testcase = "Series with Extra - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("Vol.01.cbz", new ComicInfo()
        {
            Series = "The Novel's Extra",
        });

        var library = await _scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

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
        const string testcase = "Image Series with SP Folder - Manga.json";

        var library = await _scannerHelper.GenerateScannerData(testcase);


        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

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
        const string testcase = "Image Series with SP Folder (Non English) - Image.json";

        var library = await _scannerHelper.GenerateScannerData(testcase);


        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

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

        var library = await _scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

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
        const string testcase = "PDF Comic Chapters - Comic.json";

        var library = await _scannerHelper.GenerateScannerData(testcase);


        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);
        var series = postLib.Series.First();
        Assert.Single(series.Volumes);
        Assert.Equal(4, series.Volumes.First().Chapters.Count);
    }

    [Fact]
    public async Task ScanLibrary_LooseChapters_Pdf_LN()
    {
        const string testcase = "PDF Comic Chapters - LightNovel.json";

        var library = await _scannerHelper.GenerateScannerData(testcase);


        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

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
        const string testcase = "Series with Localized - Manga.json";

        // Get the first file and generate a ComicInfo
        var infos = new Dictionary<string, ComicInfo>();
        infos.Add("My Dress-Up Darling v01.cbz", new ComicInfo()
        {
            Series = "My Dress-Up Darling",
            LocalizedSeries = "Sono Bisque Doll wa Koi wo Suru"
        });

        var library = await _scannerHelper.GenerateScannerData(testcase, infos);


        var scanner = _scannerHelper.CreateServices();
        await scanner.ScanLibrary(library.Id);
        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);

        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        var series = postLib.Series.First();
        Assert.Equal(3, series.Volumes.Count);

        // Bootstrap a new file in the nested "Sono Bisque Doll wa Koi wo Suru" directory and perform a series scan
        var testDirectory = Path.Combine(_testDirectory, Path.GetFileNameWithoutExtension(testcase));
        await _scannerHelper.Scaffold(testDirectory, ["My Dress-Up Darling/Sono Bisque Doll wa Koi wo Suru ch 11.cbz"]);

        // Now that a new file exists in the subdirectory, scan again
        await scanner.ScanSeries(series.Id);
        Assert.Single(postLib.Series);
        Assert.Equal(3, series.Volumes.Count);
        Assert.Equal(2, series.Volumes.First(v => v.MinNumber.Is(Parser.LooseLeafVolumeNumber)).Chapters.Count);
    }
}
