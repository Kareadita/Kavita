using System.Linq;
using API.DTOs;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Extensions;
using API.Services.Plus;

namespace API.Helpers.Builders;

public class PlusSeriesDtoBuilder : IEntityBuilder<PlusSeriesRequestDto>
{
    private readonly PlusSeriesRequestDto _seriesRequestDto;
    public PlusSeriesRequestDto Build() => _seriesRequestDto;

    /// <summary>
    /// This must be a FULL Series
    /// </summary>
    /// <param name="series"></param>
    public PlusSeriesDtoBuilder(Series series)
    {
        _seriesRequestDto = new PlusSeriesRequestDto()
        {
            MediaFormat = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
            SeriesName = series.Name,
            AltSeriesName = series.LocalizedName,
            AniListId = ScrobblingService.ExtractId<int?>(series.Metadata.WebLinks,
                ScrobblingService.AniListWeblinkWebsite),
            MalId = ScrobblingService.ExtractId<long?>(series.Metadata.WebLinks,
                ScrobblingService.MalWeblinkWebsite),
            GoogleBooksId = ScrobblingService.ExtractId<string?>(series.Metadata.WebLinks,
                ScrobblingService.GoogleBooksWeblinkWebsite),
            MangaDexId = ScrobblingService.ExtractId<string?>(series.Metadata.WebLinks,
                ScrobblingService.MangaDexWeblinkWebsite),
            VolumeCount = series.Volumes.Count,
            ChapterCount = series.Volumes.SelectMany(v => v.Chapters).Count(c => !c.IsSpecial),
            Year = series.Metadata.ReleaseYear
        };
    }

}
