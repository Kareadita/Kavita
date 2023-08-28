using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Search;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Responsible for the Search interface from the UI
/// </summary>
public class SearchController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;

    public SearchController(IUnitOfWork unitOfWork, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Returns the series for the MangaFile id. If the user does not have access (shouldn't happen by the UI),
    /// then null is returned
    /// </summary>
    /// <param name="mangaFileId"></param>
    /// <returns></returns>
    [HttpGet("series-for-mangafile")]
    public async Task<ActionResult<SeriesDto>> GetSeriesForMangaFile(int mangaFileId)
    {
        return Ok(await _unitOfWork.SeriesRepository.GetSeriesForMangaFile(mangaFileId, User.GetUserId()));
    }

    /// <summary>
    /// Returns the series for the Chapter id. If the user does not have access (shouldn't happen by the UI),
    /// then null is returned
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("series-for-chapter")]
    public async Task<ActionResult<SeriesDto>> GetSeriesForChapter(int chapterId)
    {
        return Ok(await _unitOfWork.SeriesRepository.GetSeriesForChapter(chapterId, User.GetUserId()));
    }

    [HttpGet("search")]
    public async Task<ActionResult<SearchResultGroupDto>> Search(string queryString)
    {
        queryString = Services.Tasks.Scanner.Parser.Parser.CleanQuery(queryString);

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null) return Unauthorized();
        var libraries = _unitOfWork.LibraryRepository.GetLibraryIdsForUserIdAsync(user.Id, QueryContext.Search).ToList();
        if (!libraries.Any()) return BadRequest(await _localizationService.Translate(User.GetUserId(), "libraries-restricted"));

        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);

        var series = await _unitOfWork.SeriesRepository.SearchSeries(user.Id, isAdmin,
            libraries, queryString);

        return Ok(series);
    }
}
