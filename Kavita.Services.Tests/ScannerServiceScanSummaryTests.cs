using Kavita.API.Services.SignalR;
using Kavita.Database.Tests;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.SignalR.Bodies;
using Kavita.Models.Metadata;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class ScannerServiceScanSummaryTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private const string Volume1 = "Spice and Wolf/Spice and Wolf Vol. 1";

    private static readonly List<string> Files =
    [
        $"{Volume1}/Spice and Wolf Vol. 1 Ch. 0001.cbz",
        $"{Volume1}/Spice and Wolf Vol. 1 Ch. 0002.cbz",
        $"{Volume1}/Spice and Wolf Vol. 1 Ch. 0003.cbz",
    ];

    private readonly IEventHub _eventHub = Substitute.For<IEventHub>();

    private async Task<(ScannerService Scanner, int LibraryId, string Root)> ScanOnce(string testcase, List<string>? files = null)
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = new ScannerHelper(unitOfWork, testOutputHelper);

        var library = await scannerHelper.GenerateScannerData($"{testcase} - Manga", files ?? Files, new Dictionary<string, ComicInfo>());
        var scanner = scannerHelper.CreateServices(eventHub: _eventHub);
        await scanner.ScanLibrary(library.Id);

        return (scanner, library.Id, library.Folders.First().Path);
    }

    private LibraryScanEndedEventBody LastSummary()
    {
        return _eventHub.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IEventHub.SendMessageAsync))
            .Select(c => c.GetArguments()[1])
            .OfType<SignalRMessageDto>()
            .Select(m => m.Body)
            .OfType<LibraryScanEndedEventBody>()
            .Last();
    }

    /// <returns>The onlyAdmins argument of each ScanSeries send</returns>
    private List<bool> ScanSeriesSends()
    {
        return _eventHub.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IEventHub.SendMessageAsync))
            .Select(c => c.GetArguments())
            .Where(args => (string) args[0]! == MessageFactory.ScanSeries)
            .Select(args => (bool) args[2]!)
            .ToList();
    }

    private List<LibraryScanProgressEventBody> ScanProgressBodies()
    {
        return _eventHub.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IEventHub.SendMessageAsync))
            .Select(c => c.GetArguments()[1])
            .OfType<SignalRMessageDto>()
            .Select(m => m.Body)
            .OfType<LibraryScanProgressEventBody>()
            .ToList();
    }

    private ScanSeriesEventBody LastScannedSeries()
    {
        return _eventHub.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IEventHub.SendMessageAsync))
            .Select(c => c.GetArguments()[1])
            .OfType<SignalRMessageDto>()
            .Select(m => m.Body)
            .OfType<ScanSeriesEventBody>()
            .Last();
    }

    // The scanner skips a folder whose write time matches the last scan to the second
    private static void TouchFolder(string root)
    {
        Directory.SetLastWriteTime(Path.Combine(root, Volume1), DateTime.Now.AddSeconds(2));
    }

    [Fact]
    public async Task FirstScan_CountsEveryChapterAsAdded()
    {
        var (_, libraryId, _) = await ScanOnce(nameof(FirstScan_CountsEveryChapterAsAdded));

        var summary = LastSummary();

        Assert.Equal(libraryId, summary.LibraryId);
        Assert.Equal(1, summary.SeriesAdded);
        Assert.Equal(3, summary.ChaptersAdded);
        Assert.Equal(0, summary.ChaptersUpdated);
        Assert.Equal(0, summary.ChaptersRemoved);
    }

    [Fact]
    public async Task ForcedRescan_WithNoDiskChange_UpdatesNothing()
    {
        var (scanner, libraryId, _) = await ScanOnce(nameof(ForcedRescan_WithNoDiskChange_UpdatesNothing));

        await scanner.ScanLibrary(libraryId, forceUpdate: true);

        var summary = LastSummary();
        Assert.Equal(0, summary.SeriesAdded);
        Assert.Equal(0, summary.ChaptersAdded);
        Assert.Equal(0, summary.ChaptersUpdated);
        Assert.Equal(0, summary.ChaptersRemoved);
    }

    [Fact]
    public async Task RescanAfterFileChanged_CountsChapterUpdated()
    {
        var (scanner, libraryId, root) = await ScanOnce(nameof(RescanAfterFileChanged_CountsChapterUpdated));

        File.SetLastWriteTime(Path.Combine(root, Files[0]), DateTime.Now.AddSeconds(2));
        TouchFolder(root);
        await scanner.ScanLibrary(libraryId);

        var summary = LastSummary();
        Assert.Equal(0, summary.ChaptersAdded);
        Assert.Equal(1, summary.ChaptersUpdated);
        Assert.Equal(0, summary.ChaptersRemoved);
    }

    [Fact]
    public async Task RescanAfterFileDeleted_CountsChapterRemoved()
    {
        var (scanner, libraryId, root) = await ScanOnce(nameof(RescanAfterFileDeleted_CountsChapterRemoved));

        File.Delete(Path.Combine(root, Files[2]));
        TouchFolder(root);
        await scanner.ScanLibrary(libraryId);

        var summary = LastSummary();
        Assert.Equal(0, summary.ChaptersAdded);
        Assert.Equal(0, summary.ChaptersUpdated);
        Assert.Equal(1, summary.ChaptersRemoved);
    }

    [Fact]
    public async Task RescanAfterSeriesFolderDeleted_CountsSeriesRemoved()
    {
        // A second series keeps the root from being empty, an empty root aborts the scan
        var (scanner, libraryId, root) = await ScanOnce(nameof(RescanAfterSeriesFolderDeleted_CountsSeriesRemoved),
            [..Files, "Accel World/Accel World v01.cbz"]);

        Directory.Delete(Path.Combine(root, "Spice and Wolf"), true);
        Directory.SetLastWriteTime(root, DateTime.Now.AddSeconds(2));
        await scanner.ScanLibrary(libraryId);

        var summary = LastSummary();
        Assert.Equal(1, summary.SeriesRemoved);
        Assert.Equal(0, summary.ChaptersAdded);
    }

    [Fact]
    public async Task FirstScan_SendsScanSeriesToReaders()
    {
        await ScanOnce(nameof(FirstScan_SendsScanSeriesToReaders));

        Assert.Equal([false], ScanSeriesSends());
    }

    [Fact]
    public async Task ForcedRescan_WithNoDiskChange_SendsScanSeriesToReaders()
    {
        var (scanner, libraryId, _) = await ScanOnce(nameof(ForcedRescan_WithNoDiskChange_SendsScanSeriesToReaders));
        _eventHub.ClearReceivedCalls();

        await scanner.ScanLibrary(libraryId, forceUpdate: true);

        Assert.Equal([false], ScanSeriesSends());
    }

    [Fact]
    public async Task LibraryScan_ProgressHasNoSeriesScan()
    {
        await ScanOnce(nameof(LibraryScan_ProgressHasNoSeriesScan));

        var bodies = ScanProgressBodies();

        Assert.NotEmpty(bodies);
        Assert.All(bodies, b => Assert.Null(b.SeriesScan));
    }

    [Fact]
    public async Task ScanSeries_EveryProgressFrameCarriesTheSeries()
    {
        var (scanner, _, _) = await ScanOnce(nameof(ScanSeries_EveryProgressFrameCarriesTheSeries));
        var scanned = LastScannedSeries();
        _eventHub.ClearReceivedCalls();

        await scanner.ScanSeries(scanned.SeriesId);

        var bodies = ScanProgressBodies();

        Assert.True(bodies.Count >= 2, "Expected the started frame and at least one per-series update");
        Assert.All(bodies, b => Assert.Equal(new SeriesScanTarget(scanned.SeriesId, "Spice and Wolf"), b.SeriesScan));
    }
}
