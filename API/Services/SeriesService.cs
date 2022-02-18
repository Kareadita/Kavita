using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.DTOs;
using API.Entities.Enums;

namespace API.Services;


public interface ISeriesService
{
    Task<SeriesDetailDto> GetSeriesDetail(int seriesId, int userId);
}

public class SeriesService : ISeriesService
{
    private readonly IUnitOfWork _unitOfWork;

    public SeriesService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// This generates all the arrays needed by the Series Detail page in the UI. It is a specialized API for the unique layout constraints.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<SeriesDetailDto> GetSeriesDetail(int seriesId, int userId)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);

        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var volumes = (await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId)).ToList();
        var chapters = volumes.SelectMany(v => v.Chapters).ToList();


        var specials = new List<ChapterDto>();
        foreach (var chapter in chapters.Where(c => c.IsSpecial))
        {
            chapter.Title = Parser.Parser.CleanSpecialTitle(chapter.Title);
            specials.Add(chapter);
        }
        return new SeriesDetailDto()
        {
            Specials = specials,
            // Don't show chapter 0 (aka single volume chapters) in the Chapters tab or books that are just single numbers (they show as volumes)
            Chapters = chapters.Where(c => !c.IsSpecial &&
                                           (!c.Number.Equals(Parser.Parser.DefaultChapter) ||
                                            (c.Number.All(c2 =>
                                                 char.IsNumber(c2) || char.IsDigit(c2)) &&
                                             libraryType == LibraryType.Book)))
                .OrderBy(c => float.Parse(c.Number), new ChapterSortComparer()),
            Volumes = volumes,
            StorylineChapters = volumes.Where(v => v.Number == 0).SelectMany(v => v.Chapters).OrderBy(c => float.Parse(c.Number), new ChapterSortComparer())

        };
    }
}
