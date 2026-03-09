using System;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus.Manage;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

/// <summary>
/// All things centered around Managing the Kavita instance, that isn't aligned with an entity
/// </summary>
[Authorize(PolicyGroups.AdminPolicy)]
public class ManageController(IUnitOfWork unitOfWork) : BaseApiController
{
    /// <summary>
    /// Returns a list of all Series that is Kavita+ applicable to metadata match and the status of it
    /// </summary>
    /// <returns></returns>
    [KPlus]
    [Authorize(PolicyGroups.AdminPolicy)]
    [HttpPost("series-metadata")]
    public async Task<ActionResult<PagedList<ManageMatchSeriesDto>>> SeriesMetadata(ManageMatchFilterDto filter, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;

        var res = await unitOfWork.ExternalSeriesMetadataRepository.GetAllSeries(filter, userParams);

        Response.AddPaginationHeader(res);
        return Ok(res);
    }
}
