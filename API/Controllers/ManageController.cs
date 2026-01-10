#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs;
using API.DTOs.KavitaPlus.Manage;
using API.Extensions;
using API.Helpers;
using API.Services.Plus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// All things centered around Managing the Kavita instance, that isn't aligned with an entity
/// </summary>
[Authorize(PolicyGroups.AdminPolicy)]
public class ManageController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILicenseService _licenseService;

    public ManageController(IUnitOfWork unitOfWork, ILicenseService licenseService)
    {
        _unitOfWork = unitOfWork;
        _licenseService = licenseService;
    }

    /// <summary>
    /// Returns a list of all Series that is Kavita+ applicable to metadata match and the status of it
    /// </summary>
    /// <returns></returns>
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("series-metadata")]
    public async Task<ActionResult<PagedList<ManageMatchSeriesDto>>> SeriesMetadata(ManageMatchFilterDto filter, [FromQuery] UserParams? userParams)
    {
        //if (!await _licenseService.HasActiveLicense()) return Ok(Array.Empty<SeriesDto>());

        userParams ??= UserParams.Default;

        var res = await _unitOfWork.ExternalSeriesMetadataRepository.GetAllSeries(filter, userParams);

        Response.AddPaginationHeader(res);
        return Ok(res);
    }
}
