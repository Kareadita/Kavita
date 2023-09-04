using System;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Filtering.v2;
using API.Entities;
using API.Extensions;
using API.Helpers;
using EasyCaching.Core;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// This is responsible for Filter caching
/// </summary>
public class FilterController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEasyCachingProviderFactory _cacheFactory;

    public FilterController(IUnitOfWork unitOfWork, IEasyCachingProviderFactory cacheFactory)
    {
        _unitOfWork = unitOfWork;
        _cacheFactory = cacheFactory;
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

        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name must be set");

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

    // TODO: Add APIs to add/update/delete filter
}
