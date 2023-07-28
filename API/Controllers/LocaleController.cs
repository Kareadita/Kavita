using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using API.Data;
using API.DTOs.Filtering;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class LocaleController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;

    public LocaleController(IUnitOfWork unitOfWork, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<string>> GetAllLocales()
    {
        var languages = _localizationService.GetLocales().Select(c => new CultureInfo(c)).Select(c =>
            new LanguageDto()
            {
                Title = c.DisplayName,
                IsoCode = c.IetfLanguageTag
            }).Where(l => !string.IsNullOrEmpty(l.IsoCode));
        return Ok(languages);
    }
}
