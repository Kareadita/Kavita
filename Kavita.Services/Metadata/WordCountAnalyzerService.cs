using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using HtmlAgilityPack;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Helpers;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.SignalR;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Reading;
using Microsoft.Extensions.Logging;
using VersOne.Epub;

namespace Kavita.Services.Metadata;

/// <summary>
/// This service is a metadata task that generates information around time to read
/// </summary>
public class WordCountAnalyzerService(
    ILogger<WordCountAnalyzerService> logger,
    IUnitOfWork unitOfWork,
    IEventHub eventHub,
    ICacheHelper cacheHelper,
    IMediaErrorService mediaErrorService)
    : IWordCountAnalyzerService
{
    public const int AverageCharactersPerWord = 5;


    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60 * 60)]
    [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ScanLibrary(int libraryId, bool forceUpdate = false, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, ct: ct);
        if (library == null) return;

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(libraryId, 0F, ProgressEventType.Started, string.Empty), ct: ct);

        var chunkInfo = await unitOfWork.SeriesRepository.GetChunkInfoAsync(library.Id, ct);
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("[MetadataService] Refreshing Library {LibraryName}. Total Items: {TotalSize}. Total Chunks: {TotalChunks} with {ChunkSize} size", library.Name, chunkInfo.TotalSize, chunkInfo.TotalChunks, chunkInfo.ChunkSize);

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(library.Id, 0F, ProgressEventType.Started, $"Starting {library.Name}"), ct: ct);

        for (var chunk = 1; chunk <= chunkInfo.TotalChunks; chunk++)
        {
            if (chunkInfo.TotalChunks == 0) continue;
            stopwatch.Restart();

            logger.LogInformation("[MetadataService] Processing chunk {ChunkNumber} / {TotalChunks} with size {ChunkSize}. Series ({SeriesStart} - {SeriesEnd}",
                chunk, chunkInfo.TotalChunks, chunkInfo.ChunkSize, chunk * chunkInfo.ChunkSize, (chunk + 1) * chunkInfo.ChunkSize);

            var nonLibrarySeries = await unitOfWork.SeriesRepository.GetFullSeriesForLibraryIdAsync(library.Id,
                new UserParams()
                {
                    PageNumber = chunk,
                    PageSize = chunkInfo.ChunkSize
                }, ct);
            logger.LogDebug("[MetadataService] Fetched {SeriesCount} series for refresh", nonLibrarySeries.Count);

            var seriesIndex = 0;
            foreach (var series in nonLibrarySeries)
            {
                var index = chunk * seriesIndex;
                var progress =  Math.Max(0F, Math.Min(1F, index * 1F / chunkInfo.TotalSize));

                await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                    MessageFactory.WordCountAnalyzerProgressEvent(library.Id, progress, ProgressEventType.Updated, series.Name), ct: ct);

                try
                {
                    await ProcessSeries(series, forceUpdate, false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[MetadataService] There was an exception during metadata refresh for {SeriesName}", series.Name);
                }
                seriesIndex++;
            }

            if (unitOfWork.HasChanges())
            {
                await unitOfWork.CommitAsync(ct);
            }

            logger.LogInformation(
                "[MetadataService] Processed {SeriesStart} - {SeriesEnd} out of {TotalSeries} series in {ElapsedScanTime} milliseconds for {LibraryName}",
                chunk * chunkInfo.ChunkSize, (chunk * chunkInfo.ChunkSize) + nonLibrarySeries.Count, chunkInfo.TotalSize, stopwatch.ElapsedMilliseconds, library.Name);
        }

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(library.Id, 1F, ProgressEventType.Ended, $"Complete"), ct: ct);


        logger.LogInformation("[WordCountAnalyzerService] Updated metadata for {LibraryName} in {ElapsedMilliseconds} milliseconds", library.Name, sw.ElapsedMilliseconds);

    }

    public async Task ScanSeries(int libraryId, int seriesId, bool forceUpdate = true, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var series = await unitOfWork.SeriesRepository.GetFullSeriesForSeriesIdAsync(seriesId, ct);
        if (series == null)
        {
            logger.LogError("[WordCountAnalyzerService] Series {SeriesId} was not found on Library {LibraryId}", seriesId, libraryId);
            return;
        }

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(libraryId, 0F, ProgressEventType.Started, series.Name), ct: ct);

        await ProcessSeries(series, forceUpdate);

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync(ct);
        }

        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(libraryId, 1F, ProgressEventType.Ended, series.Name), ct: ct);

        logger.LogInformation("[WordCountAnalyzerService] Updated metadata for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
    }


    private async Task ProcessSeries(Series series, bool forceUpdate = false, bool useFileName = true)
    {
        var isEpub = series.Format == MangaFormat.Epub;
        var existingWordCount = series.WordCount;
        series.WordCount = 0;
        foreach (var volume in series.Volumes)
        {
            volume.WordCount = 0;
            foreach (var chapter in volume.Chapters)
            {
                // This compares if it's changed since a file scan only
                var firstFile = chapter.Files.FirstOrDefault();
                if (firstFile == null || !cacheHelper.HasFileChangedSinceLastScan(firstFile.LastFileAnalysis,
                        forceUpdate,
                        firstFile))
                {
                    volume.WordCount += chapter.WordCount;
                    series.WordCount += chapter.WordCount;
                    continue;
                }

                if (series.Format == MangaFormat.Epub)
                {
                    long sum = 0;
                    var fileCounter = 1;
                    foreach (var file in chapter.Files)
                    {
                        var filePath = file.FilePath;
                        var pageCounter = 1;
                        try
                        {
                            // default: Replace with BookService method, we will loose progress but these tasks are usually fast
                            using var book = await EpubReader.OpenBookAsync(filePath, BookService.LenientBookReaderOptions);

                            var totalPages = book.Content.Html.Local;
                            foreach (var bookPage in totalPages)
                            {
                                var progress = Math.Max(0F,
                                    Math.Min(1F, (fileCounter * pageCounter) * 1F / (chapter.Files.Count * totalPages.Count)));

                                await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                                    MessageFactory.WordCountAnalyzerProgressEvent(series.LibraryId, progress,
                                        ProgressEventType.Updated, useFileName ? filePath : series.Name));
                                sum += await GetWordCountFromHtml(bookPage, filePath);
                                pageCounter++;
                            }

                            fileCounter++;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "There was an error reading an epub file for word count, series skipped");
                            await eventHub.SendMessageAsync(MessageFactory.Error,
                                MessageFactory.ErrorEvent("There was an issue counting words on an epub",
                                    $"{series.Name} - {file.FilePath}"));
                            return;
                        }

                        UpdateFileAnalysis(file);
                    }

                    chapter.WordCount = sum;
                    series.WordCount += sum;
                    volume.WordCount += sum;
                }

                var est = ReaderService.GetTimeEstimate(chapter.WordCount, chapter.Pages, isEpub);
                chapter.MinHoursToRead = est.MinHours;
                chapter.MaxHoursToRead = est.MaxHours;
                chapter.AvgHoursToRead = est.AvgHours;

                foreach (var file in chapter.Files)
                {
                    UpdateFileAnalysis(file);
                }
                unitOfWork.ChapterRepository.Update(chapter);
            }

            var volumeEst = ReaderService.GetTimeEstimate(volume.WordCount, volume.Pages, isEpub);
            volume.MinHoursToRead = volumeEst.MinHours;
            volume.MaxHoursToRead = volumeEst.MaxHours;
            volume.AvgHoursToRead = volumeEst.AvgHours;
            unitOfWork.VolumeRepository.Update(volume);

        }

        if (series.WordCount == 0 && existingWordCount != 0) series.WordCount = existingWordCount; // Restore original word count if the file hasn't changed
        var seriesEstimate = ReaderService.GetTimeEstimate(series.WordCount, series.Pages, isEpub);
        series.MinHoursToRead = seriesEstimate.MinHours;
        series.MaxHoursToRead = seriesEstimate.MaxHours;
        series.AvgHoursToRead = seriesEstimate.AvgHours;
        unitOfWork.SeriesRepository.Update(series);
    }

    private void UpdateFileAnalysis(MangaFile file)
    {
        file.UpdateLastFileAnalysis();
        unitOfWork.MangaFileRepository.Update(file);
    }

    private async Task<int> GetWordCountFromHtml(EpubLocalTextContentFileRef bookFile, string filePath)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(await bookFile.ReadContentAsync());

            var textNodes = doc.DocumentNode.SelectNodes("//body//text()[not(parent::script)]");
            var characterCount =  textNodes?.Sum(node => node.InnerText.Count(char.IsLetter)) ?? 0;
            return GetWordCount(characterCount);
        }
        catch (EpubContentException ex)
        {
            logger.LogError(ex, "Error when counting words in epub {EpubPath}", filePath);
            await mediaErrorService.ReportMediaIssueAsync(filePath, MediaErrorProducer.BookService,
                $"Invalid Epub Metadata, {bookFile.FilePath} does not exist", ex.Message);
            return 0;
        }
    }

    public static int GetWordCount(int characterCount)
    {
        if (characterCount == 0) return 0;
        return characterCount / AverageCharactersPerWord;
    }

}
