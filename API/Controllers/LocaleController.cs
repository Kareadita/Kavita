using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using API.DTOs.Filtering;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace API.Controllers;

#nullable enable

public class LocaleController : BaseApiController
{
    private readonly ILocalizationService _localizationService;
    private readonly IHostEnvironment _environment;

    public LocaleController(ILocalizationService localizationService, IHostEnvironment environment)
    {
        _localizationService = localizationService;
        _environment = environment;
    }

    /// <summary>
    /// Returns all applicable locales on the server
    /// </summary>
    /// <remarks>This can be cached as it will not change per version.</remarks>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet]
    public ActionResult<IEnumerable<KavitaLocale>> GetAllLocales()
    {
        // TODO: Add caching against version number
        if (!_environment.IsDevelopment())
        {

        }

        return Ok(_localizationService.GetLocales().Where(l => l.TranslationCompletion > 0f));
    }
}
