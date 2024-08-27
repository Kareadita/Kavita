using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using API.DTOs.Filtering;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

public class LocaleController : BaseApiController
{
    private readonly ILocalizationService _localizationService;

    public LocaleController(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<string>> GetAllLocales()
    {
        // Check if temp/locale_map.json exists

        // If not, scan the 2 locale files and calculate empty keys or empty values

        // Formulate the Locale object with Percentage
        var languages = _localizationService.GetLocales().Select(c =>
            {
                try
                {
                    var cult = new CultureInfo(c);
                    return new LanguageDto()
                    {
                        Title = cult.DisplayName,
                        IsoCode = cult.IetfLanguageTag
                    };
                }
                catch (Exception ex)
                {
                    // Some OS' don't have all culture codes supported like PT_BR, thus we need to default
                    return new LanguageDto()
                    {
                        Title = c,
                        IsoCode = c
                    };
                }
            })
            .Where(l => !string.IsNullOrEmpty(l.IsoCode))
            .OrderBy(d => d.Title);
        return Ok(languages);
    }
}
