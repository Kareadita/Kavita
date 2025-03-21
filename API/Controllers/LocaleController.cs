using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.DTOs.Filtering;
using API.Services;
using EasyCaching.Core;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace API.Controllers;

#nullable enable

public class LocaleController : BaseApiController
{
    private readonly ILocalizationService _localizationService;
    private readonly IEasyCachingProvider _localeCacheProvider;

    private static readonly string CacheKey = "locales_" + BuildInfo.Version;

    public LocaleController(ILocalizationService localizationService, IEasyCachingProviderFactory cachingProviderFactory)
    {
        _localizationService = localizationService;
        _localeCacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.LocaleOptions);
    }

    /// <summary>
    /// Returns all applicable locales on the server
    /// </summary>
    /// <remarks>This can be cached as it will not change per version.</remarks>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<KavitaLocale>>> GetAllLocales()
    {
        var result = await _localeCacheProvider.GetAsync<IEnumerable<KavitaLocale>>(CacheKey);
        if (result.HasValue)
        {
            return Ok(result.Value);
        }

        var ret = _localizationService.GetLocales().Where(l => l.TranslationCompletion > 0f);
        await _localeCacheProvider.SetAsync(CacheKey, ret, TimeSpan.FromDays(7));

        return Ok();
    }
}
