using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Helpers;
using API.SignalR;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

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

        // TODO: Figure out how to accomplish this


        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.WordCountAnalyzerProgressEvent(libraryId, 1F, ProgressEventType.Ended, series.Name));

        _logger.LogInformation("[WordCountAnalyzerService] Updated metadata for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
    }

    private static int GetWordCountFromHtml(string fileContents)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(fileContents);
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
