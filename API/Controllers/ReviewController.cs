using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.SeriesDetail;
using API.Extensions;
using API.Services.Plus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

public class ReviewController : BaseApiController
{
    private readonly ILogger<ReviewController> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILicenseService _licenseService;

    public ReviewController(ILogger<ReviewController> logger, IUnitOfWork unitOfWork, ILicenseService licenseService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _licenseService = licenseService;
    }


    /// <summary>
    /// Fetches reviews from the server for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserReviewDto>>> GetReviews(int seriesId)
    {
        var userRatings = await _unitOfWork.UserRepository.GetUserRatingDtosForSeriesAsync(seriesId);
        if (!await _licenseService.DefaultUserHasLicense() || !await _licenseService.HasActiveLicense(User.GetUserId()))
        {
            return Ok(userRatings);
        }

        // Fetch external reviews and splice them in
        return Ok(userRatings);
    }
}
