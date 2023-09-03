using System;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Filtering.v2;
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

    [HttpGet]
    public async Task<ActionResult<FilterV2Dto?>> GetFilter(string name)
    {
        var provider = _cacheFactory.GetCachingProvider(EasyCacheProfiles.Filter);
        if (string.IsNullOrEmpty(name)) return Ok(null);
        var filter = await provider.GetAsync<FilterV2Dto>(name);
        if (filter.HasValue)
        {
            filter.Value.Name = name;
            return Ok(filter.Value);
        }

        return Ok(null);
    }

    /// <summary>
    /// Caches the filter in the backend and returns a temp string for retrieving.
    /// </summary>
    /// <remarks>The cache line lives for only 1 hour</remarks>
    /// <param name="filterDto"></param>
    /// <returns></returns>
    [HttpPost("create-temp")]
    public async Task<ActionResult<string>> CreateTempFilter(FilterV2Dto filterDto)
    {
        var provider = _cacheFactory.GetCachingProvider(EasyCacheProfiles.Filter);
        var name = filterDto.Name;
        if (string.IsNullOrEmpty(filterDto.Name))
        {
            name = Guid.NewGuid().ToString();
        }

        await provider.SetAsync(name, filterDto, TimeSpan.FromHours(1));
        return name;
    }
}
