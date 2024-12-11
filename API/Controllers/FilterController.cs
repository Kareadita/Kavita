using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Dashboard;
using API.DTOs.Filtering.v2;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

/// <summary>
/// This is responsible for Filter caching
/// </summary>
public class FilterController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;

    public FilterController(IUnitOfWork unitOfWork, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
    }

    /// <summary>
    /// Creates or Updates the filter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    public async Task<ActionResult> CreateOrUpdateSmartFilter(FilterV2Dto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId(), AppUserIncludes.SmartFilters);
        if (user == null) return Unauthorized();
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name must be set");
        if (Seed.DefaultStreams.Any(s => s.Name.Equals(dto.Name, StringComparison.InvariantCultureIgnoreCase)))
        {
            return BadRequest("You cannot use the name of a system provided stream");
        }

        var existingFilter =
            user.SmartFilters.FirstOrDefault(f => f.Name.Equals(dto.Name, StringComparison.InvariantCultureIgnoreCase));
        if (existingFilter != null)
        {
            // Update the filter
            existingFilter.Filter = SmartFilterHelper.Encode(dto);
            _unitOfWork.AppUserSmartFilterRepository.Update(existingFilter);
        }
        else
        {
            existingFilter = new AppUserSmartFilter()
            {
                Name = dto.Name,
                Filter = SmartFilterHelper.Encode(dto)
            };
            user.SmartFilters.Add(existingFilter);
            _unitOfWork.UserRepository.Update(user);
        }

        if (!_unitOfWork.HasChanges()) return Ok();
        await _unitOfWork.CommitAsync();

        return Ok();
    }

    [HttpGet]
    public ActionResult<IEnumerable<SmartFilterDto>> GetFilters()
    {
        return Ok(_unitOfWork.AppUserSmartFilterRepository.GetAllDtosByUserId(User.GetUserId()));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteFilter(int filterId)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        var filter = await _unitOfWork.AppUserSmartFilterRepository.GetById(filterId);
        if (filter == null) return Ok();
        // This needs to delete any dashboard filters that have it too
        var streams = await _unitOfWork.UserRepository.GetDashboardStreamWithFilter(filter.Id);
        _unitOfWork.UserRepository.Delete(streams);

        var streams2 = await _unitOfWork.UserRepository.GetSideNavStreamWithFilter(filter.Id);
        _unitOfWork.UserRepository.Delete(streams2);

        _unitOfWork.AppUserSmartFilterRepository.Delete(filter);
        await _unitOfWork.CommitAsync();
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
}
