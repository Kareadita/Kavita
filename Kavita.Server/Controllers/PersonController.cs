using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.SignalR;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Metadata.Browse;
using Kavita.Models.DTOs.Metadata.Browse.Requests;
using Kavita.Models.DTOs.Person;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Kavita.Services.Plus;
using Kavita.Services.Scanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;
public class PersonController(
    IUnitOfWork unitOfWork,
    ILocalizationService localizationService,
    IMapper mapper,
    ICoverDbService coverDbService,
    IImageService imageService,
    IEventHub eventHub,
    IPersonService personService)
    : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<PersonDto>> GetPersonByName(string name)
    {
        var person = await unitOfWork.PersonRepository.GetPersonDtoByName(name, UserId);
        if (person == null) return NotFound();

        person.Roles = (await unitOfWork.PersonRepository.GetRolesForPersonByName(person.Id, UserId)).ToList();

        EnrichWithWebLinks(person);

        return Ok(person);
    }

    /// <summary>
    /// Populate <see cref="PersonDto.WebLinks"/> from set ids
    /// </summary>
    /// <param name="personDto"></param>
    /// <remarks><see cref="PersonDto.Roles"/> must be set for this to work</remarks>
    private static void EnrichWithWebLinks(PersonDto personDto)
    {
        if (personDto.Roles == null) return;

        var isCharacter = personDto.Roles.Count == 1 && personDto.Roles.Contains(PersonRole.Character);
        personDto.WebLinks = [];

        if (personDto.AniListId != 0)
        {
            var urlPrefix = isCharacter ? ScrobblingService.AniListCharacterWebsite : ScrobblingService.AniListStaffWebsite;
            personDto.WebLinks.Add($"{urlPrefix}{personDto.AniListId}");
        }

        if (personDto.MalId != 0)
        {
            var urlPrefix = isCharacter ? ScrobblingService.MalCharacterWebsite : ScrobblingService.MalStaffWebsite;
            personDto.WebLinks.Add($"{urlPrefix}{personDto.MalId}");
        }

        // Hardcover currently does not seem to have characters
        if (!string.IsNullOrEmpty(personDto.HardcoverId) && !isCharacter)
        {
            personDto.WebLinks.Add($"{ScrobblingService.HardcoverStaffWebsite}{personDto.HardcoverId}");
        }
    }

    /// <summary>
    /// Find a person by name or alias against a query string
    /// </summary>
    /// <param name="queryString"></param>
    /// <returns></returns>
    [HttpGet("search")]
    public async Task<ActionResult<List<PersonDto>>> SearchPeople([FromQuery] string queryString)
    {
        return Ok(await unitOfWork.PersonRepository.SearchPeople(queryString));
    }

    /// <summary>
    /// Returns all roles for a Person
    /// </summary>
    /// <param name="personId"></param>
    /// <returns></returns>
    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<PersonRole>>> GetRolesForPersonByName(int personId)
    {
        return Ok(await unitOfWork.PersonRepository.GetRolesForPersonByName(personId, UserId));
    }


    /// <summary>
    /// Returns a list of authors and artists for browsing
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="userParams"></param>
    /// <returns></returns>
    [HttpPost("all")]
    public async Task<ActionResult<PagedList<BrowsePersonDto>>> GetPeopleForBrowse(BrowsePersonFilterDto filter, [FromQuery] UserParams? userParams)
    {
        userParams ??= UserParams.Default;

        var list = await unitOfWork.PersonRepository.GetBrowsePersonDtos(UserId, filter, userParams);
        Response.AddPaginationHeader(list.CurrentPage, list.PageSize, list.TotalCount, list.TotalPages);

        return Ok(list);
    }

    /// <summary>
    /// Updates the Person
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<PersonDto>> UpdatePerson(UpdatePersonDto dto)
    {
        // This needs to get all people and update them equally
        var person = await unitOfWork.PersonRepository.GetPersonById(dto.Id, PersonIncludes.Aliases);
        if (person == null) return BadRequest(localizationService.Translate(UserId, "person-doesnt-exist"));

        if (string.IsNullOrEmpty(dto.Name)) return BadRequest(await localizationService.Translate(UserId, "person-name-required"));


        // Validate the name is unique
        if (dto.Name != person.Name && !(await unitOfWork.PersonRepository.IsNameUnique(dto.Name)))
        {
            return BadRequest(await localizationService.Translate(UserId, "person-name-unique"));
        }

        // Update name first, in case it got moved to aliases
        person.Name = dto.Name.Trim();
        person.NormalizedName = person.Name.ToNormalized();

        var success = await personService.UpdatePersonAliasesAsync(person, dto.Aliases);
        if (!success) return BadRequest(await localizationService.Translate(UserId, "aliases-have-overlap"));


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
        if (!string.IsNullOrEmpty(asin) && Parser.IsLikelyValidAsin(asin))
        {
            person.Asin = asin;
        }

        unitOfWork.PersonRepository.Update(person);
        await unitOfWork.CommitAsync();

        return Ok(mapper.Map<PersonDto>(person));
    }

    /// <summary>
    /// Attempts to download the cover from CoversDB (Note: Not yet release in Kavita)
    /// </summary>
    /// <param name="personId"></param>
    /// <returns></returns>
    [PersonAccess]
    [HttpPost("fetch-cover")]
    public async Task<ActionResult<string>> DownloadCoverImage([FromQuery] int personId)
    {
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var person = await unitOfWork.PersonRepository.GetPersonById(personId);
        if (person == null) return BadRequest(localizationService.Translate(UserId, "person-doesnt-exist"));

        var personImage = await coverDbService.DownloadPersonImageAsync(person, settings.EncodeMediaAs);

        if (string.IsNullOrEmpty(personImage))
        {

            return BadRequest(await localizationService.Translate(UserId, "person-image-doesnt-exist"));
        }

        person.CoverImage = personImage;
        imageService.UpdateColorScape(person);
        unitOfWork.PersonRepository.Update(person);

        await unitOfWork.CommitAsync();
        await eventHub.SendMessageAsync(MessageFactory.CoverUpdate, MessageFactory.CoverUpdateEvent(person.Id, "person"), false);

        return Ok(personImage);
    }

    /// <summary>
    /// Returns the top 20 series that the "person" is known for. This will use Average Rating when applicable (Kavita+ field), else it's a random sort
    /// </summary>
    /// <param name="personId"></param>
    /// <returns></returns>
    [PersonAccess]
    [HttpGet("series-known-for")]
    public async Task<ActionResult<IEnumerable<SeriesDto>>> GetKnownSeries(int personId)
    {
        return Ok(await unitOfWork.PersonRepository.GetSeriesKnownFor(personId, UserId));
    }


    /// <summary>
    /// Returns all individual chapters by role. Limited to 20 results.
    /// </summary>
    /// <param name="personId"></param>
    /// <param name="role"></param>
    /// <returns></returns>
    [PersonAccess]
    [HttpGet("chapters-by-role")]
    public async Task<ActionResult<IEnumerable<StandaloneChapterDto>>> GetChaptersByRole(int personId, PersonRole role)
    {
        return Ok(await unitOfWork.PersonRepository.GetChaptersForPersonByRole(personId, UserId, role));
    }

    /// <summary>
    /// Merges Persons into one, this action is irreversible
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("merge")]
    [Authorize(PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<PersonDto>> MergePeople(PersonMergeDto dto)
    {
        var dst = await unitOfWork.PersonRepository.GetPersonById(dto.DestId, PersonIncludes.All);
        if (dst == null) return BadRequest();

        var src = await unitOfWork.PersonRepository.GetPersonById(dto.SrcId, PersonIncludes.All);
        if (src == null) return BadRequest();

        await personService.MergePeopleAsync(src, dst);
        await eventHub.SendMessageAsync(MessageFactory.PersonMerged, MessageFactory.PersonMergedMessage(dst, src));

        return Ok(mapper.Map<PersonDto>(dst));
    }

    /// <summary>
    /// Ensure the alias is valid to be added. For example, the alias cannot be on another person or be the same as the current person name/alias.
    /// </summary>
    /// <param name="dto">alias check request</param>
    /// <returns></returns>
    [HttpPost("valid-alias")]
    public async Task<ActionResult<bool>> IsValidAlias(PersonAliasCheckDto dto)
    {
        var person = await unitOfWork.PersonRepository.GetPersonById(dto.PersonId, PersonIncludes.Aliases);
        if (person == null) return NotFound();

        var aliasIsName = dto.Name.ToNormalized() == dto.Alias.ToNormalized();
        var existingAlias = await unitOfWork.PersonRepository.AnyAliasExist(dto.Alias);

        return Ok(!existingAlias && !aliasIsName);
    }


}
