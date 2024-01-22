using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Extensions;
using API.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

public class RecommendedController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public const string CacheKey = "recommendation_";

    public RecommendedController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Quick Reads are series that should be readable in less than 10 in total and are not Ongoing in release.
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="userParams">Pagination</param>
    /// <returns></returns>
    [HttpGet("quick-reads")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetQuickReads(int libraryId, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;
        var series = await _unitOfWork.SeriesRepository.GetQuickReads(User.GetUserId(), libraryId, userParams);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    /// <summary>
    /// Quick Catchup Reads are series that should be readable in less than 10 in total and are Ongoing in release.
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="userParams"></param>
    /// <returns></returns>
    [HttpGet("quick-catchup-reads")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetQuickCatchupReads(int libraryId, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;
        var series = await _unitOfWork.SeriesRepository.GetQuickCatchupReads(User.GetUserId(), libraryId, userParams);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    /// <summary>
    /// Highly Rated based on other users ratings. Will pull series with ratings > 4.0, weighted by count of other users.
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="userParams">Pagination</param>
    /// <returns></returns>
    [HttpGet("highly-rated")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetHighlyRated(int libraryId, [FromQuery] UserParams? userParams)
    {
        var userId = User.GetUserId();
        userParams ??= UserParams.Default;
        var series = await _unitOfWork.SeriesRepository.GetHighlyRated(userId, libraryId, userParams);
        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);
        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    /// <summary>
    /// Chooses a random genre and shows series that are in that without reading progress
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="genreId">Genre Id</param>
    /// <param name="userParams">Pagination</param>
    /// <returns></returns>
    [HttpGet("more-in")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetMoreIn(int libraryId, int genreId, [FromQuery] UserParams? userParams)
    {
        var userId = User.GetUserId();

        userParams ??= UserParams.Default;
        var series = await _unitOfWork.SeriesRepository.GetMoreIn(userId, libraryId, genreId, userParams);
        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, series);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    /// <summary>
    /// Series that are fully read by the user in no particular order
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="userParams">Pagination</param>
    /// <returns></returns>
    [HttpGet("rediscover")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetRediscover(int libraryId, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;
        var series = await _unitOfWork.SeriesRepository.GetRediscover(User.GetUserId(), libraryId, userParams);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

}
