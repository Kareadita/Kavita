using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Extensions;
using API.Helpers;
using API.Services.Plus;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class RecommendedController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecommendationService _recommendationService;
    private readonly ILicenseService _licenseService;

    public RecommendedController(IUnitOfWork unitOfWork, IRecommendationService recommendationService,
        ILicenseService licenseService)
    {
        _unitOfWork = unitOfWork;
        _recommendationService = recommendationService;
        _licenseService = licenseService;
    }

    /// <summary>
    /// For KavitaPlus users, this will return recommendations on the server.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("recommendations")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetRecommendations(int seriesId)
    {
        var userId = User.GetUserId();
        if (!await _licenseService.DefaultUserHasLicense() || !await _licenseService.HasActiveLicense(userId))
        {
            return Ok(new List<SeriesDto>());
        }

        if (!await _unitOfWork.UserRepository.HasAccessToSeries(userId, seriesId))
        {
            return BadRequest("User does not have access to this Series");
        }
        return Ok(await _recommendationService.GetRecommendationsForSeries(userId, seriesId));
    }


    /// <summary>
    /// Quick Reads are series that should be readable in less than 10 in total and are not Ongoing in release.
    /// </summary>
    /// <param name="libraryId">Library to restrict series to</param>
    /// <param name="userParams">Pagination</param>
    /// <returns></returns>
    [HttpGet("quick-reads")]
    public async Task<ActionResult<PagedList<SeriesDto>>> GetQuickReads(int libraryId, [FromQuery] UserParams userParams)
    {
        userParams ??= new UserParams();
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
    public async Task<ActionResult<PagedList<SeriesDto>>> GetQuickCatchupReads(int libraryId, [FromQuery] UserParams userParams)
    {
        userParams ??= new UserParams();
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
    public async Task<ActionResult<PagedList<SeriesDto>>> GetHighlyRated(int libraryId, [FromQuery] UserParams userParams)
    {
        var userId = User.GetUserId()!;
        userParams ??= new UserParams();
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
    public async Task<ActionResult<PagedList<SeriesDto>>> GetMoreIn(int libraryId, int genreId, [FromQuery] UserParams userParams)
    {
        var userId = User.GetUserId();

        userParams ??= new UserParams();
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
    public async Task<ActionResult<PagedList<SeriesDto>>> GetRediscover(int libraryId, [FromQuery] UserParams userParams)
    {
        userParams ??= new UserParams();
        var series = await _unitOfWork.SeriesRepository.GetRediscover(User.GetUserId(), libraryId, userParams);

        Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);
        return Ok(series);
    }

}
