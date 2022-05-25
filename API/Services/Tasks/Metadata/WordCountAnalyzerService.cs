using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.SignalR;
using Hangfire;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using VersOne.Epub;

namespace API.Services.Tasks.Metadata;

public interface IWordCountAnalyzerService
{
    Task ScanLibrary(int libraryId, bool forceUpdate = false);
    Task ScanSeries(int libraryId, int seriesId, bool forceUpdate = false);
}

/// <summary>
/// This service is a metadata task that generates information around time to read
/// </summary>
public class WordCountAnalyzerService : IWordCountAnalyzerService
{
    private readonly ILogger<WordCountAnalyzerService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly ICacheHelper _cacheHelper;

    public WordCountAnalyzerService(ILogger<WordCountAnalyzerService> logger, IUnitOfWork unitOfWork, IEventHub eventHub,
        ICacheHelper cacheHelper)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _cacheHelper = cacheHelper;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 360)]
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScanLibrary(int libraryId, bool forceUpdate = false)
    {
        var sw = Stopwatch.StartNew();
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.None);

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(libraryId, 0F, ProgressEventType.Started, string.Empty));

        var chunkInfo = await _unitOfWork.SeriesRepository.GetChunkInfo(library.Id);
        var stopwatch = Stopwatch.StartNew();
        var totalTime = 0L;
        _logger.LogInformation("[MetadataService] Refreshing Library {LibraryName}. Total Items: {TotalSize}. Total Chunks: {TotalChunks} with {ChunkSize} size", library.Name, chunkInfo.TotalSize, chunkInfo.TotalChunks, chunkInfo.ChunkSize);

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(library.Id, 0F, ProgressEventType.Started, $"Starting {library.Name}"));

        for (var chunk = 1; chunk <= chunkInfo.TotalChunks; chunk++)
        {
            if (chunkInfo.TotalChunks == 0) continue;
            totalTime += stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();

            _logger.LogInformation("[MetadataService] Processing chunk {ChunkNumber} / {TotalChunks} with size {ChunkSize}. Series ({SeriesStart} - {SeriesEnd}",
                chunk, chunkInfo.TotalChunks, chunkInfo.ChunkSize, chunk * chunkInfo.ChunkSize, (chunk + 1) * chunkInfo.ChunkSize);

            var nonLibrarySeries = await _unitOfWork.SeriesRepository.GetFullSeriesForLibraryIdAsync(library.Id,
                new UserParams()
                {
                    PageNumber = chunk,
                    PageSize = chunkInfo.ChunkSize
                });
            _logger.LogDebug("[MetadataService] Fetched {SeriesCount} series for refresh", nonLibrarySeries.Count);

            var seriesIndex = 0;
            foreach (var series in nonLibrarySeries)
            {
                var index = chunk * seriesIndex;
                var progress =  Math.Max(0F, Math.Min(1F, index * 1F / chunkInfo.TotalSize));

                await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                    MessageFactory.WordCountAnalyzerProgressEvent(library.Id, progress, ProgressEventType.Updated, series.Name));

                try
                {
                    await ProcessSeries(series, forceUpdate, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MetadataService] There was an exception during metadata refresh for {SeriesName}", series.Name);
                }
                seriesIndex++;
            }

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync();
            }

            _logger.LogInformation(
                "[MetadataService] Processed {SeriesStart} - {SeriesEnd} out of {TotalSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                chunk * chunkInfo.ChunkSize, (chunk * chunkInfo.ChunkSize) + nonLibrarySeries.Count, chunkInfo.TotalSize, stopwatch.ElapsedMilliseconds, library.Name);
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(library.Id, 1F, ProgressEventType.Ended, $"Complete"));


        _logger.LogInformation("[WordCountAnalyzerService] Updated metadata for {LibraryName} in {ElapsedMilliseconds} milliseconds", library.Name, sw.ElapsedMilliseconds);

    }

    public async Task ScanSeries(int libraryId, int seriesId, bool forceUpdate = false)
    {
        var sw = Stopwatch.StartNew();
        var series = await _unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId);
        if (series == null)
        {
            _logger.LogError("[WordCountAnalyzerService] Series {SeriesId} was not found on Library {LibraryId}", seriesId, libraryId);
            return;
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(libraryId, 0F, ProgressEventType.Started, series.Name));

        await ProcessSeries(series);

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(libraryId, 1F, ProgressEventType.Ended, series.Name));

        _logger.LogInformation("[WordCountAnalyzerService] Updated metadata for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
    }

    private async Task ProcessSeries(Series series, bool forceUpdate = false, bool useFileName = true)
    {
        if (series.Format != MangaFormat.Epub) return;

        long totalSum = 0;

        foreach (var chapter in series.Volumes.SelectMany(v => v.Chapters))
        {
            // This compares if it's changed since a file scan only
            if (!_cacheHelper.HasFileNotChangedSinceCreationOrLastScan(chapter, false,
                    chapter.Files.FirstOrDefault()) && chapter.WordCount != 0)
                continue;

            long sum = 0;
            var fileCounter = 1;
            foreach (var file in chapter.Files.Select(file => file.FilePath))
            {
                var pageCounter = 1;
                try
                {
                    using var book = await EpubReader.OpenBookAsync(file, BookService.BookReaderOptions);

                    var totalPages = book.Content.Html.Values;
                    foreach (var bookPage in totalPages)
                    {
                        var progress = Math.Max(0F,
                            Math.Min(1F, (fileCounter * pageCounter) * 1F / (chapter.Files.Count * totalPages.Count)));

                        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                            MessageFactory.WordCountAnalyzerProgressEvent(series.LibraryId, progress,
                                ProgressEventType.Updated, useFileName ? file : series.Name));
                        sum += await GetWordCountFromHtml(bookPage);
                        pageCounter++;
                    }

                    fileCounter++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "There was an error reading an epub file for word count, series skipped");
                    await _eventHub.SendMessageAsync(MessageFactory.Error,
                        MessageFactory.ErrorEvent("There was an issue counting words on an epub",
                            $"{series.Name} - {file}"));
                    return;
                }

            }

            chapter.WordCount = sum;
            _unitOfWork.ChapterRepository.Update(chapter);
            totalSum += sum;
        }

        series.WordCount = totalSum;
        _unitOfWork.SeriesRepository.Update(series);
    }


    private static async Task<int> GetWordCountFromHtml(EpubContentFileRef bookFile)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(await bookFile.ReadContentAsTextAsync());

        var textNodes = doc.DocumentNode.SelectNodes("//body//text()[not(parent::script)]");
        if (textNodes == null) return 0;

        return textNodes
            .Select(node => node.InnerText)
            .Select(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => char.IsLetter(s[0])))
            .Select(words => words.Count())
            .Where(wordCount => wordCount > 0)
            .Sum();
    }


}
