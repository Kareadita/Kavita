using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
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
}

/// <param name="SeriesId">Null when nothing was saved</param>
public sealed record ProcessSeriesResult(int? SeriesId, bool SeriesAdded, int ChaptersAdded, int ChaptersUpdated, int ChaptersRemoved)
{
    public static readonly ProcessSeriesResult NotSaved = new(null, false, 0, 0, 0);
}

public interface IProcessSeries
{
    Task<ProcessSeriesResult> ProcessSeriesAsync(MetadataSettingsDto settings, IList<ParserInfo> parsedInfos, ProcessSeriesArgs args);
}
