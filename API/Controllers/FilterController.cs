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

    // TODO: Add APIs to add/update/delete filter
}
