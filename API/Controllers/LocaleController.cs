using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class LocaleController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private static readonly IReadOnlyList<string> AllLocales = new List<string>() { "en" };

    public LocaleController(IUnitOfWork unitOfWork, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<string>> GetAllLocales()
    {
        return Ok(AllLocales);
    }
}
