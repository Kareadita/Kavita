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

    public Task ScanLibrary(int libraryId, bool forceUpdate = false)
    {
        return Task.CompletedTask;
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
            if (!_cacheHelper.HasFileNotChangedSinceCreationOrLastScan(chapter, false, chapter.Files.FirstOrDefault()))
                continue;

            long sum = 0;
            foreach (var file in chapter.Files)
            {
                await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                    MessageFactory.WordCountAnalyzerProgressEvent(series.LibraryId, 1F, ProgressEventType.Updated, file.FilePath));


                using var book = await EpubReader.OpenBookAsync(file.FilePath, BookService.BookReaderOptions);
                foreach (var bookFile in book.Content.Html.Values)
                {
                    sum += await GetWordCountFromHtml(bookFile);
                }
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
