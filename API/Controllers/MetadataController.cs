using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.Metadata;
using API.Entities.Enums;
using API.Extensions;
using Kavita.Common.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;


public class MetadataController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public MetadataController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Fetches genres from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all genres</param>
    /// <returns></returns>
    [HttpGet("genres")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Instant, VaryByQueryKeys = new []{"libraryIds"})]
    public async Task<ActionResult<IList<GenreTagDto>>> GetAllGenres(string? libraryIds)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        var ids = libraryIds?.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.GenreRepository.GetAllGenreDtosForLibrariesAsync(ids, userId));
        }

        return Ok(await _unitOfWork.GenreRepository.GetAllGenreDtosAsync(userId));
    }



    /// <summary>
    /// Fetches people from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all people</param>
    /// <returns></returns>
    [HttpGet("people")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Instant, VaryByQueryKeys = new []{"libraryIds"})]
    public async Task<ActionResult<IList<PersonDto>>> GetAllPeople(string? libraryIds)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        var ids = libraryIds?.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.PersonRepository.GetAllPeopleDtosForLibrariesAsync(ids, userId));
        }
        return Ok(await _unitOfWork.PersonRepository.GetAllPersonDtosAsync(userId));
    }

    /// <summary>
    /// Fetches all tags from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all tags</param>
    /// <returns></returns>
    [HttpGet("tags")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Instant, VaryByQueryKeys = new []{"libraryIds"})]
    public async Task<ActionResult<IList<TagDto>>> GetAllTags(string? libraryIds)
    {
        var userId = await _unitOfWork.UserRepository.GetUserIdByUsernameAsync(User.GetUsername());
        var ids = libraryIds?.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.TagRepository.GetAllTagDtosForLibrariesAsync(ids, userId));
        }
        return Ok(await _unitOfWork.TagRepository.GetAllTagDtosAsync(userId));
    }

    /// <summary>
    /// Fetches all age ratings from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all ratings</param>
    /// <remarks>This API is cached for 1 hour, varying by libraryIds</remarks>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute, VaryByQueryKeys = new [] {"libraryIds"})]
    [HttpGet("age-ratings")]
    public async Task<ActionResult<IList<AgeRatingDto>>> GetAllAgeRatings(string? libraryIds)
    {
        var ids = libraryIds?.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.LibraryRepository.GetAllAgeRatingsDtosForLibrariesAsync(ids));
        }

        return Ok(Enum.GetValues<AgeRating>().Select(t => new AgeRatingDto()
        {
            Title = t.ToDescription(),
            Value = t
        }).Where(r => r.Value > AgeRating.NotApplicable));
    }

    /// <summary>
    /// Fetches all publication status' from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all publication status</param>
    /// <remarks>This API is cached for 1 hour, varying by libraryIds</remarks>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute, VaryByQueryKeys = new [] {"libraryIds"})]
    [HttpGet("publication-status")]
    public ActionResult<IList<AgeRatingDto>> GetAllPublicationStatus(string? libraryIds)
    {
        var ids = libraryIds?.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids is {Count: > 0})
        {
            return Ok(_unitOfWork.LibraryRepository.GetAllPublicationStatusesDtosForLibrariesAsync(ids));
        }

        return Ok(Enum.GetValues<PublicationStatus>().Select(t => new PublicationStatusDto()
        {
            Title = t.ToDescription(),
            Value = t
        }).OrderBy(t => t.Title));
    }

    /// <summary>
    /// Fetches all age languages from the libraries passed (or if none passed, all in the server)
    /// </summary>
    /// <remarks>This does not perform RBS for the user if they have Library access due to the non-sensitive nature of languages</remarks>
    /// <param name="libraryIds">String separated libraryIds or null for all ratings</param>
    /// <returns></returns>
    [HttpGet("languages")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Instant, VaryByQueryKeys = new []{"libraryIds"})]
    public async Task<ActionResult<IList<LanguageDto>>> GetAllLanguages(string? libraryIds)
    {
        var ids = libraryIds?.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids is {Count: > 0})
        {
            return Ok(await _unitOfWork.LibraryRepository.GetAllLanguagesForLibrariesAsync(ids));
        }


        return Ok(await _unitOfWork.LibraryRepository.GetAllLanguagesForLibrariesAsync());
    }

    [HttpGet("all-languages")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Hour)]
    public IEnumerable<LanguageDto> GetAllValidLanguages()
    {
        return CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c =>
            new LanguageDto()
            {
                Title = c.DisplayName,
                IsoCode = c.IetfLanguageTag
            }).Where(l => !string.IsNullOrEmpty(l.IsoCode));
    }

    /// <summary>
    /// Returns summary for the chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("chapter-summary")]
    public async Task<ActionResult<string>> GetChapterSummary(int chapterId)
    {
        if (chapterId <= 0) return BadRequest("Chapter does not exist");
        var chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
        if (chapter == null) return BadRequest("Chapter does not exist");
        return Ok(chapter.Summary);
    }
}
