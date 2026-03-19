using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Search;
using Kavita.Server.Attributes;
using Kavita.Services.Scanner;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

/// <summary>
/// Responsible for the Search interface from the UI
/// </summary>
public class SearchController(IUnitOfWork unitOfWork, ILocalizationService localizationService,
    IEntityNamingService namingService) : BaseApiController
{
    /// <summary>
    /// Returns the series for the MangaFile id. If the user does not have access (shouldn't happen by the UI),
    /// then null is returned
    /// </summary>
    /// <param name="mangaFileId"></param>
    /// <returns></returns>
    [HttpGet("series-for-mangafile")]
    public async Task<ActionResult<SeriesDto>> GetSeriesForMangaFile(int mangaFileId)
    {
        var series = await unitOfWork.SeriesRepository.GetSeriesForMangaFile(mangaFileId, UserId);
        if (series == null) return NotFound();

        if (!await unitOfWork.UserRepository.HasAccessToSeries(UserId, series.Id))
            return NotFound();

        return Ok(series);
    }

    /// <summary>
    /// Returns the series for the Chapter id. If the user does not have access (shouldn't happen by the UI),
    /// then null is returned
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("series-for-chapter")]
    public async Task<ActionResult<SeriesDto>> GetSeriesForChapter(int chapterId)
    {
        return Ok(await unitOfWork.SeriesRepository.GetSeriesForChapter(chapterId, UserId));
    }

    /// <summary>
    /// Searches against different entities in the system against a query string
    /// </summary>
    /// <param name="queryString"></param>
    /// <param name="includeChapterAndFiles">Include Chapter and Filenames in the entities. This can slow down the search on larger systems</param>
    /// <returns></returns>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResultGroupDto>> Search(string queryString, [FromQuery] bool includeChapterAndFiles = true)
    {
        queryString = Parser.CleanQuery(queryString);

        var libraries = await unitOfWork.LibraryRepository.GetLibraryIdsForUserIdAsync(UserId, QueryContext.Search);
        if (libraries.Count == 0) return BadRequest(await localizationService.Translate(UserId, "libraries-restricted"));

        var isAdmin = UserContext.HasRole(PolicyConstants.AdminRole);

        var series = await unitOfWork.SeriesRepository.SearchSeries(UserId, isAdmin,
            libraries, queryString, includeChapterAndFiles);

        return Ok(series);
    }

    /// <summary>
    /// Returns all chapters for a given series with localized titles. Used for CBL chapter-level matching.
    /// </summary>
    [HttpGet("chapters-by-series")]
    public async Task<ActionResult<IList<ChapterDto>>> GetChaptersBySeries([FromQuery] int seriesId)
    {
        if (!await unitOfWork.UserRepository.HasAccessToSeries(UserId, seriesId))
            return Unauthorized();

        var libraryType = await unitOfWork.LibraryRepository.GetLibraryTypeBySeriesIdAsync(seriesId);
        var volumes = await unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, UserId);
        var namingContext = await LocalizedNamingContext.CreateAsync(namingService, localizationService, UserId, libraryType);

        var chapters = volumes
            .SelectMany(v => v.Chapters.Select(c =>
            {
                c.VolumeTitle = namingContext.FormatVolumeName(v) ?? v.Name;
                c.Title = namingContext.FormatChapterTitle(c);
                return c;
            }))
            .OrderBy(c => c.SortOrder)
            .ToList();

        return Ok(chapters);
    }
}
