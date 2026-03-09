using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Models;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.Entities.User;
using Kavita.Server.Attributes;
using Kavita.Services;
using Kavita.Services.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

public class FilterController(
    IUnitOfWork unitOfWork,
    ILocalizationService localizationService,
    IStreamService streamService,
    ILogger<FilterController> logger)
    : BaseApiController
{
    /// <summary>
    /// Creates or Updates the filter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> CreateOrUpdateSmartFilter(FilterV2Dto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.SmartFilters);
        if (user == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name must be set");
        if (Defaults.DefaultStreams.Any(s => s.Name.Equals(dto.Name, StringComparison.InvariantCultureIgnoreCase)))
        {
            return BadRequest("You cannot use the name of a system provided stream");
        }

        var existingFilter =
            user.SmartFilters.FirstOrDefault(f => f.Name.Equals(dto.Name, StringComparison.InvariantCultureIgnoreCase));
        if (existingFilter != null)
        {
            // Update the filter
            existingFilter.Filter = SmartFilterHelper.Encode(dto);
            unitOfWork.AppUserSmartFilterRepository.Update(existingFilter);
        }
        else
        {
            existingFilter = new AppUserSmartFilter()
            {
                Name = dto.Name,
                Filter = SmartFilterHelper.Encode(dto)
            };
            user.SmartFilters.Add(existingFilter);
            unitOfWork.UserRepository.Update(user);
        }

        if (!unitOfWork.HasChanges()) return Ok();
        await unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// All Smart Filters for the authenticated user
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SmartFilterDto>>> GetFilters()
    {
        return Ok(await unitOfWork.AppUserSmartFilterRepository.GetAllDtosByUserId(UserId));
    }

    /// <summary>
    /// Delete the smart filter for the authenticated user
    /// </summary>
    /// <remarks>User must not be in <see cref="PolicyConstants.ReadOnlyRole"/></remarks>
    /// <param name="filterId"></param>
    /// <returns></returns>
    [HttpDelete]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteFilter(int filterId)
    {
        var filter = await unitOfWork.AppUserSmartFilterRepository.GetById(filterId);
        if (filter == null) return Ok();
        // This needs to delete any dashboard filters that have it too
        var streams = await unitOfWork.UserRepository.GetDashboardStreamWithFilter(filter.Id);
        unitOfWork.UserRepository.Delete(streams);

        var streams2 = await unitOfWork.UserRepository.GetSideNavStreamWithFilter(filter.Id);
        unitOfWork.UserRepository.Delete(streams2);

        unitOfWork.AppUserSmartFilterRepository.Delete(filter);
        await unitOfWork.CommitAsync();
        return Ok();
    }

    /// <summary>
    /// Encode the Filter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("encode")]
    public ActionResult<string> EncodeFilter(FilterV2Dto dto)
    {
        return Ok(SmartFilterHelper.Encode(dto));
    }

    /// <summary>
    /// Decodes the Filter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("decode")]
    public ActionResult<FilterV2Dto> DecodeFilter(DecodeFilterDto dto)
    {
        return Ok(SmartFilterHelper.Decode(dto.EncodedFilter));
    }

    /// <summary>
    /// Rename a Smart Filter given the filterId and new name
    /// </summary>
    /// <param name="filterId"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    [HttpPost("rename")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> RenameFilter([FromQuery] int filterId, [FromQuery] string name)
    {
        try
        {
            var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId,
                AppUserIncludes.SmartFilters);
            if (user == null) return Unauthorized();

            name = name.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(await localizationService.Translate(user.Id, "smart-filter-name-required"));
            }

            if (Defaults.DefaultStreams.Any(s => s.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
            {
                return BadRequest(await localizationService.Translate(user.Id, "smart-filter-system-name"));
            }

            var filter = user.SmartFilters.FirstOrDefault(f => f.Id == filterId);
            if (filter == null)
            {
                return BadRequest(await localizationService.Translate(user.Id, "filter-not-found"));
            }

            filter.Name = name;
            unitOfWork.AppUserSmartFilterRepository.Update(filter);
            await unitOfWork.CommitAsync();

            await streamService.RenameSmartFilterStreams(filter);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when renaming smart filter: {FilterId}", filterId);
            return BadRequest(await localizationService.Translate(UserId, "generic-error"));
        }

    }
}
