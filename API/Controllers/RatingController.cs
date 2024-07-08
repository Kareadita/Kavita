using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs;
using API.Extensions;
using API.Services.Plus;
using EasyCaching.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controllers;

#nullable enable

/// <summary>
/// Responsible for providing external ratings for Series
/// </summary>
public class RatingController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public RatingController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;

    }

    [HttpGet("overall")]
    public async Task<ActionResult<RatingDto>> GetOverallRating(int seriesId)
    {
        return Ok(new RatingDto()
        {
            Provider = ScrobbleProvider.Kavita,
            AverageScore = await _unitOfWork.SeriesRepository.GetAverageUserRating(seriesId, User.GetUserId()),
            FavoriteCount = 0
        });
    }
}
