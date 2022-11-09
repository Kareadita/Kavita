using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.DTOs.Search;
using API.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Responsible for the Search interface from the UI
/// </summary>
public class SearchController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public SearchController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.SeriesRepository.GetSeriesForMangaFile(mangaFileId, userId));
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
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        return Ok(await _unitOfWork.SeriesRepository.GetSeriesForChapter(chapterId, userId));
    }

    /// <summary>
    /// Search for different entities against the query string
    /// </summary>
    /// <param name="queryString"></param>
    /// <returns></returns>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResultGroupDto>> Search(string queryString)
    {
        queryString = Uri.UnescapeDataString(queryString)
            .Trim()
            .Replace(@"%", string.Empty)
            .Replace(":", string.Empty);
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

        // Get libraries user has access to
        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).ToList();
        if (!libraries.Any()) return BadRequest("User does not have access to any libraries");

        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);
        var series = await _unitOfWork.SeriesRepository.SearchSeries(user.Id, isAdmin,
            libraries.Select(l => l.Id).ToArray(), queryString);

        return Ok(series);
    }
}
