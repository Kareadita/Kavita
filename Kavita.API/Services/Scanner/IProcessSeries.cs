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
}

public interface IProcessSeries
{
    Task<int?> ProcessSeriesAsync(MetadataSettingsDto settings, IList<ParserInfo> parsedInfos, ProcessSeriesArgs args);
}
