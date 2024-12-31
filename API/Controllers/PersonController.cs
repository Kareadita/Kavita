using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Tasks.Metadata;
using API.SignalR;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nager.ArticleNumber;

namespace API.Controllers;
#nullable enable

public class PersonController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly IMapper _mapper;
    private readonly ICoverDbService _coverDbService;
    private readonly IImageService _imageService;
    private readonly IEventHub _eventHub;

    public PersonController(IUnitOfWork unitOfWork, ILocalizationService localizationService, IMapper mapper,
        ICoverDbService coverDbService, IImageService imageService, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _mapper = mapper;
        _coverDbService = coverDbService;
        _imageService = imageService;
        _eventHub = eventHub;
    }


    [HttpGet]
    public async Task<ActionResult<PersonDto>> GetPersonByName(string name)
    {
        return Ok(await _unitOfWork.PersonRepository.GetPersonDtoByName(name, User.GetUserId()));
    }

    /// <summary>
    /// Returns all roles for a Person
    /// </summary>
    /// <param name="personId"></param>
    /// <returns></returns>
    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<PersonRole>>> GetRolesForPersonByName(int personId)
    {
        return Ok(await _unitOfWork.PersonRepository.GetRolesForPersonByName(personId, User.GetUserId()));
    }

    /// <summary>
    /// Returns a list of authors & artists for browsing
    /// </summary>
    /// <param name="userParams"></param>
    /// <returns></returns>
    [HttpPost("all")]
    public async Task<ActionResult<PagedList<BrowsePersonDto>>> GetAuthorsForBrowse([FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;
        var list = await _unitOfWork.PersonRepository.GetAllWritersAndSeriesCount(User.GetUserId(), userParams);
        Response.AddPaginationHeader(list.CurrentPage, list.PageSize, list.TotalCount, list.TotalPages);
        return Ok(list);
    }

    /// <summary>
    /// Updates the Person
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [Authorize("RequireAdminRole")]
    [HttpPost("update")]
    public async Task<ActionResult<PersonDto>> UpdatePerson(UpdatePersonDto dto)
    {
        // This needs to get all people and update them equally
        var person = await _unitOfWork.PersonRepository.GetPersonById(dto.Id);
        if (person == null) return BadRequest(_localizationService.Translate(User.GetUserId(), "person-doesnt-exist"));

        if (string.IsNullOrEmpty(dto.Name)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "person-name-required"));


        // Validate the name is unique
        if (dto.Name != person.Name && !(await _unitOfWork.PersonRepository.IsNameUnique(dto.Name)))
        {
            return BadRequest(await _localizationService.Translate(User.GetUserId(), "person-name-unique"));
        }

        person.Name = dto.Name?.Trim();
        person.Description = dto.Description ?? string.Empty;
        person.CoverImageLocked = dto.CoverImageLocked;

        if (dto.MalId is > 0)
        {
            person.MalId = (long) dto.MalId;
        }
        if (dto.AniListId is > 0)
        {
            person.AniListId = (int) dto.AniListId;
        }

        if (!string.IsNullOrEmpty(dto.HardcoverId?.Trim()))
        {
            person.HardcoverId = dto.HardcoverId.Trim();
        }

        var asin = dto.Asin?.Trim();
        if (!string.IsNullOrEmpty(asin) &&
            (ArticleNumberHelper.IsValidIsbn10(asin) || ArticleNumberHelper.IsValidIsbn13(asin)))
        {
            person.Asin = asin;
        }

        _unitOfWork.PersonRepository.Update(person);
        await _unitOfWork.CommitAsync();

        return Ok(_mapper.Map<PersonDto>(person));
    }

    /// <summary>
    /// Attempts to download the cover from CoversDB (Note: Not yet release in Kavita)
    /// </summary>
    /// <param name="personId"></param>
    /// <returns></returns>
    [HttpPost("fetch-cover")]
    public async Task<ActionResult<string>> DownloadCoverImage([FromQuery] int personId)
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var person = await _unitOfWork.PersonRepository.GetPersonById(personId);
        if (person == null) return BadRequest(_localizationService.Translate(User.GetUserId(), "person-doesnt-exist"));

        var personImage = await _coverDbService.DownloadPersonImageAsync(person, settings.EncodeMediaAs);

        if (string.IsNullOrEmpty(personImage))
        {

            return BadRequest(await _localizationService.Translate(User.GetUserId(), "person-image-doesnt-exist"));
        }

        person.CoverImage = personImage;
        _imageService.UpdateColorScape(person);
        _unitOfWork.PersonRepository.Update(person);
        await _unitOfWork.CommitAsync();
        await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate, MessageFactory.CoverUpdateEvent(person.Id, "person"), false);

        return Ok(personImage);
    }

    /// <summary>
    /// Returns the top 20 series that the "person" is known for. This will use Average Rating when applicable (Kavita+ field), else it's a random sort
    /// </summary>
    /// <param name="personId"></param>
    /// <returns></returns>
    [HttpGet("series-known-for")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetKnownSeries(int personId)
    {
        return Ok(await _unitOfWork.PersonRepository.GetSeriesKnownFor(personId));
    }

    /// <summary>
    /// Returns all individual chapters by role. Limited to 20 results.
    /// </summary>
    /// <param name="personId"></param>
    /// <param name="role"></param>
    /// <returns></returns>
    [HttpGet("chapters-by-role")]
    public async Task<ActionResult<IEnumerable<StandaloneChapterDto>>> GetChaptersByRole(int personId, PersonRole role)
    {
        return Ok(await _unitOfWork.PersonRepository.GetChaptersForPersonByRole(personId, User.GetUserId(), role));
    }


}
