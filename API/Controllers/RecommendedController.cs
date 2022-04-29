using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Extensions;
using API.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class RecommendedController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public RecommendedController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }


    /// <summary>
    /// Quick Reads are series that are less than 2K pages in total.
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <returns></returns>
    [HttpGet("quick-reads")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetQuickReads(int libraryId, [FromQuery] UserParams userParams)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

        userParams ??= new UserParams();
        var series = await _unitOfWork.SeriesRepository.GetQuickReads(user.Id, libraryId, userParams);
        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    /// <summary>
    /// Highly Rated based on other users ratings. Will pull series with ratings > 4.0, weighted by count of other users.
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <returns></returns>
    [HttpGet("highly-rated")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetHighlyRated(int libraryId, [FromQuery] UserParams userParams)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

        userParams ??= new UserParams();
        var series = await _unitOfWork.SeriesRepository.GetHighlyRated(user.Id, libraryId, userParams);
        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    /// <summary>
    /// Chooses a random genre and shows series that are in that without reading progress
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <returns></returns>
    [HttpGet("more-in")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetMoreIn(int libraryId, int genreId, [FromQuery] UserParams userParams)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

        userParams ??= new UserParams();
        var series = await _unitOfWork.SeriesRepository.GetMoreIn(user.Id, libraryId, genreId, userParams);
        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

    /// <summary>
    /// Series that are fully read by the user in no particular order
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <returns></returns>
    [HttpGet("rediscover")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetRediscover(int libraryId, [FromQuery] UserParams userParams)
    {
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

        userParams ??= new UserParams();
        var series = await _unitOfWork.SeriesRepository.GetRediscover(user.Id, libraryId, userParams);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

}
