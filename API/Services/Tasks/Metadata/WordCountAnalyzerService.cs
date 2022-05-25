using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.SignalR;
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

    public async Task ScanLibrary(int libraryId, bool forceUpdate = false)
    {
        var allSeries = await _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(libraryId);

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

    private async Task ProcessSeries(Series series)
    {
        if (series.Format != MangaFormat.Epub) return;

        long totalSum = 0;

        foreach (var chapter in series.Volumes.SelectMany(v => v.Chapters))
        {
            // This compares if it's changed since a file scan only
            if (!_cacheHelper.HasFileNotChangedSinceCreationOrLastScan(chapter, false, chapter.Files.FirstOrDefault()))
                continue;

            long sum = 0;
            var fileCounter = 1;
            foreach (var file in chapter.Files)
            {
                var pageCounter = 1;
                using var book = await EpubReader.OpenBookAsync(file.FilePath, BookService.BookReaderOptions);

                var totalPages = book.Content.Html.Values;
                foreach (var bookPage in totalPages)
                {
                    var progress = Math.Max(0F,
                        Math.Min(1F, (fileCounter * pageCounter) * 1F / (chapter.Files.Count * totalPages.Count)));

                    await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                        MessageFactory.WordCountAnalyzerProgressEvent(series.LibraryId, progress, ProgressEventType.Updated, file.FilePath));
                    sum += await GetWordCountFromHtml(bookPage);
                    pageCounter++;
                }

                fileCounter++;
            }

            chapter.WordCount = sum;
            _unitOfWork.ChapterRepository.Update(chapter);
            totalSum += sum;
        }

        series.WordCount = totalSum;
        _unitOfWork.SeriesRepository.Update(series);

        // TODO: Hook in ICacheHelper to not recalculate if file hasn't changed AND WordCount != 0

    }


    private static async Task<int> GetWordCountFromHtml(EpubContentFileRef bookFile)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(await bookFile.ReadContentAsTextAsync());
        var delimiter = new char[] {' '};

        return doc.DocumentNode.SelectNodes("//body//text()[not(parent::script)]")
            .Select(node => node.InnerText)
            .Select(text => text.Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => char.IsLetter(s[0])))
            .Select(words => words.Count())
            .Where(wordCount => wordCount > 0)
            .Sum();
    }


}
