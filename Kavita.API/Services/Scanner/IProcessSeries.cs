using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.SignalR.Bodies;
using Kavita.Models.Entities;
using Kavita.Models.Parser;

namespace Kavita.API.Services.Scanner;

public sealed record ProcessSeriesArgs
{
    public required Library Library { get; init; }
    public required int TotalToProcess { get; init; }
    public required int LeftToProcess { get; init; }
    public bool ForceUpdate { get; init; } = false;
    /// <summary>
    /// Taken before the walk, stored as the series' LastFolderScanned
    /// </summary>
    public required DateTime ScanStarted { get; init; }
    /// <summary>
    /// Null on a library scan
    /// </summary>
    public SeriesScanTargetDto? SeriesScan { get; init; }
}

/// <param name="SeriesId">Null when nothing was saved</param>
/// <param name="Failed">The update threw or could not be saved, as opposed to having nothing to process</param>
public sealed record ProcessSeriesResult(int? SeriesId, bool SeriesAdded, int ChaptersAdded, int ChaptersUpdated, int ChaptersRemoved,
    bool Failed = false)
{
    public static readonly ProcessSeriesResult NotSaved = new(null, false, 0, 0, 0);
    public static readonly ProcessSeriesResult UpdateFailed = new(null, false, 0, 0, 0, Failed: true);
}

public interface IProcessSeries
{
    Task<ProcessSeriesResult> ProcessSeriesAsync(MetadataSettingsDto settings, IList<ParserInfo> parsedInfos, ProcessSeriesArgs args);
}
