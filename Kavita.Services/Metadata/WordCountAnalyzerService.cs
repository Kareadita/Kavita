using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Reading;
using Kavita.Services.Scanner;
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
    IMediaErrorService mediaErrorService,
    IDirectoryService directoryService)
    : IWordCountAnalyzerService
{
    public const int AverageCharactersPerWord = 5;


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


    private async Task ProcessSeries(Series series, bool forceUpdate)
    {
        var isEpub = series.Format == MangaFormat.Epub;
        var existingWordCount = series.WordCount;
        series.WordCount = 0;
        foreach (var volume in series.Volumes)
        {
            volume.WordCount = 0;
            foreach (var chapter in volume.Chapters)
            {
                if (isEpub && chapter.Files.Count > 0 && (forceUpdate || chapter.Files.Any(HasFileChangedSinceAnalysis)))
                {
                    long sum = 0;
                    var fileCounter = 1;
                    foreach (var file in chapter.Files)
                    {
                        var filePath = file.FilePath;
                        var pageCounter = 1;
                        try
                        {
                            using var book = await EpubReader.OpenBookAsync(filePath, BookService.LenientBookReaderOptions);

                            var totalPages = book.Content.Html.Local;
                            foreach (var bookPage in totalPages)
                            {
                                var progress = Math.Max(0F,
                                    Math.Min(1F, (fileCounter * pageCounter) * 1F / (chapter.Files.Count * totalPages.Count)));

                                await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                                    MessageFactory.WordCountAnalyzerProgressEvent(series.LibraryId, progress,
                                        ProgressEventType.Updated, filePath));
                                sum += await GetWordCountFromHtml(bookPage, filePath);
                                pageCounter++;
                            }

                            fileCounter++;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "There was an error reading an epub file for word count, series skipped");
                            await eventHub.SendMessageAsync(MessageFactory.Error,
                                MessageFactory.WordCountFailedEvent(series.LibraryId, series.Id, series.Name, file.FilePath));
                            return;
                        }

                        UpdateFileAnalysis(file);
                    }

                    chapter.WordCount = sum;
                }

                volume.WordCount += chapter.WordCount;
                series.WordCount += chapter.WordCount;

                var est = ReaderService.GetTimeEstimate(chapter.WordCount, chapter.Pages, isEpub);
                chapter.MinHoursToRead = est.MinHours;
                chapter.MaxHoursToRead = est.MaxHours;
                chapter.AvgHoursToRead = est.AvgHours;
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
        var sourceFile = directoryService.FileSystem.FileInfo.New(file.FilePath);
        if (sourceFile.Exists)
        {
            file.AnalyzedFileWriteTimeUtc = sourceFile.LastWriteTimeUtc;
        }
        unitOfWork.MangaFileRepository.Update(file);
    }

    /// <remarks>A null stamp (not counted since v0.9.2) counts as changed, so each file is counted once more</remarks>
    private bool HasFileChangedSinceAnalysis(MangaFile file)
    {
        var sourceFile = directoryService.FileSystem.FileInfo.New(file.FilePath);
        if (!sourceFile.Exists) return false;
        return file.AnalyzedFileWriteTimeUtc == null ||
               !FolderChangeCheck.IsSameWriteTime(file.AnalyzedFileWriteTimeUtc.Value, sourceFile.LastWriteTimeUtc);
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
                MediaErrorReason.WordCountFailed, $"{bookFile.FilePath}: {ParseIssues.Describe(ex)}");
            return 0;
        }
    }

    public static int GetWordCount(int characterCount)
    {
        if (characterCount == 0) return 0;
        return characterCount / AverageCharactersPerWord;
    }

}
