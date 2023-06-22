using API.Services.Plus;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Responsible for providing external ratings for Series
/// </summary>
public class RatingController : BaseApiController
{
    private readonly ILicenseService _licenseService;

    public RatingController(ILicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    [HttpGet]
    public ActionResult<int> GetRating(int seriesId)
    {
        return Ok(10);
    }
}
