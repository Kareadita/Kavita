using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Filtering;
using API.DTOs.Metadata;
using API.DTOs.Metadata.Browse;
using API.DTOs.Person;
using API.DTOs.SeriesDetail;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Plus;
using Kavita.Common.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;
#nullable enable

public class MetadataController(IUnitOfWork unitOfWork, IExternalMetadataService metadataService) : BaseApiController
{
    /// <summary>
    /// Fetches genres from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all genres</param>
    /// <param name="context">Context from which this API was invoked</param>
    /// <returns></returns>
    [HttpGet("genres")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Instant, VaryByQueryKeys = ["libraryIds", "context"])]
    public async Task<ActionResult<IList<GenreTagDto>>> GetAllGenres(string? libraryIds, QueryContext context = QueryContext.None)
    {
        var ids = libraryIds?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToList();

        return Ok(await unitOfWork.GenreRepository.GetAllGenreDtosForLibrariesAsync(UserId, ids, context));
    }

    /// <summary>
    /// Returns a list of Genres with counts for counts when Genre is on Series/Chapter
    /// </summary>
    /// <returns></returns>
    [HttpPost("genres-with-counts")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute)]
    public async Task<ActionResult<PagedList<BrowseGenreDto>>> GetBrowseGenres(UserParams? userParams = null)
    {
        userParams ??= UserParams.Default;

        var list = await unitOfWork.GenreRepository.GetBrowseableGenre(UserId, userParams);
        Response.AddPaginationHeader(list.CurrentPage, list.PageSize, list.TotalCount, list.TotalPages);

        return Ok(list);
    }

    /// <summary>
    /// Fetches people from the instance by role
    /// </summary>
    /// <param name="role">role</param>
    /// <returns></returns>
    [HttpGet("people-by-role")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Instant, VaryByQueryKeys = ["role"])]
    public async Task<ActionResult<IList<PersonDto>>> GetAllPeople(PersonRole? role)
    {
        return role.HasValue ?
            Ok(await unitOfWork.PersonRepository.GetAllPersonDtosByRoleAsync(UserId, role.Value)) :
            Ok(await unitOfWork.PersonRepository.GetAllPersonDtosAsync(UserId));
    }

    /// <summary>
    /// Fetches people from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all people</param>
    /// <returns></returns>
    [HttpGet("people")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Instant, VaryByQueryKeys = ["libraryIds"])]
    public async Task<ActionResult<IList<PersonDto>>> GetAllPeople(string? libraryIds)
    {
        var ids = libraryIds?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids is {Count: > 0})
        {
            return Ok(await unitOfWork.PersonRepository.GetAllPeopleDtosForLibrariesAsync(UserId, ids));
        }

        return Ok(await unitOfWork.PersonRepository.GetAllPeopleDtosForLibrariesAsync(UserId));
    }

    /// <summary>
    /// Fetches all tags from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all tags</param>
    /// <returns></returns>
    [HttpGet("tags")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Instant, VaryByQueryKeys = ["libraryIds"])]
    public async Task<ActionResult<IList<TagDto>>> GetAllTags(string? libraryIds)
    {
        var ids = libraryIds?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids is {Count: > 0})
        {
            return Ok(await unitOfWork.TagRepository.GetAllTagDtosForLibrariesAsync(UserId, ids));
        }
        return Ok(await unitOfWork.TagRepository.GetAllTagDtosForLibrariesAsync(UserId));
    }

    /// <summary>
    /// Returns a list of Tags with counts for counts when Tag is on Series/Chapter
    /// </summary>
    /// <returns></returns>
    [HttpPost("tags-with-counts")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute)]
    public async Task<ActionResult<PagedList<BrowseTagDto>>> GetBrowseTags(UserParams? userParams = null)
    {
        userParams ??= UserParams.Default;

        var list = await unitOfWork.TagRepository.GetBrowseableTag(UserId, userParams);
        Response.AddPaginationHeader(list.CurrentPage, list.PageSize, list.TotalCount, list.TotalPages);

        return Ok(list);
    }

    /// <summary>
    /// Fetches all age ratings from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all ratings</param>
    /// <remarks>This API is cached for 1 hour, varying by libraryIds</remarks>
    /// <returns></returns>
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute, VaryByQueryKeys = ["libraryIds"])]
    [HttpGet("age-ratings")]
    public async Task<ActionResult<IList<AgeRatingDto>>> GetAllAgeRatings(string? libraryIds)
    {
        var ids = libraryIds?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids is {Count: > 0})
        {
            return Ok(await unitOfWork.LibraryRepository.GetAllAgeRatingsDtosForLibrariesAsync(ids));
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
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute, VaryByQueryKeys = ["libraryIds"])]
    [HttpGet("publication-status")]
    public ActionResult<IList<AgeRatingDto>> GetAllPublicationStatus(string? libraryIds)
    {
        var ids = libraryIds?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids is {Count: > 0})
        {
            return Ok(unitOfWork.LibraryRepository.GetAllPublicationStatusesDtosForLibrariesAsync(ids));
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
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute, VaryByQueryKeys = ["libraryIds"])]
    public async Task<ActionResult<IList<LanguageDto>>> GetAllLanguages(string? libraryIds)
    {
        var ids = libraryIds?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        return Ok(await unitOfWork.LibraryRepository.GetAllLanguagesForLibrariesAsync(ids));
    }

    /// <summary>
    /// Returns all languages Kavita can accept
    /// </summary>
    /// <returns></returns>
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
    /// Given a language code returns the display name
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    [HttpGet("language-title")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Month, VaryByQueryKeys = ["code"])]
    public ActionResult<string?> GetLanguageTitle(string code)
    {
        if (string.IsNullOrEmpty(code)) return BadRequest("Code must be provided");

        return CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where(l => code.Equals(l.IetfLanguageTag))
            .Select(c => c.DisplayName)
            .FirstOrDefault();
    }

    /// <summary>
    /// Fetches the details needed from Kavita+ for Series Detail page
    /// </summary>
    /// <remarks>This will hit upstream K+ if the data in local db is 2 weeks old</remarks>
    /// <param name="seriesId">Series Id</param>
    /// <param name="libraryType">Library Type</param>
    /// <returns></returns>
    [HttpGet("series-detail-plus")]
    public async Task<ActionResult<SeriesDetailPlusDto>> GetKavitaPlusSeriesDetailData(int seriesId, LibraryType libraryType)
    {
        var userReviews = (await unitOfWork.UserRepository.GetUserRatingDtosForSeriesAsync(seriesId, UserId))
            .Where(r => !string.IsNullOrEmpty(r.Body))
            .OrderByDescending(review => review.Username.Equals(Username!) ? 1 : 0)
            .ToList();

        var ret = await metadataService.GetSeriesDetailPlus(seriesId, libraryType);

        await PrepareSeriesDetail(userReviews, ret);
        return Ok(ret);
    }

    private async Task PrepareSeriesDetail(List<UserReviewDto> userReviews, SeriesDetailPlusDto? ret)
    {
        var isAdmin = User.IsInRole(PolicyConstants.AdminRole);
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId)!;

        if (ret != null)
        {
            userReviews.AddRange(ReviewHelper.SelectSpectrumOfReviews(ret.Reviews.ToList()));
            ret.Reviews = userReviews;
        }

        if (!isAdmin && ret?.Recommendations != null && user != null)
        {
            // Re-obtain owned series and take into account age restriction
            var seriesIds = ret.Recommendations.OwnedSeries.Select(s => s.Id);
            ret.Recommendations.OwnedSeries =
                await unitOfWork.SeriesRepository.GetSeriesDtoByIdsAsync(seriesIds, user);
            ret.Recommendations.ExternalSeries = [];
        }

        if (ret?.Recommendations != null && user != null)
        {
            ret.Recommendations.OwnedSeries ??= [];
        }
    }
}
