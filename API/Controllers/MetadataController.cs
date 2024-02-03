using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Misc;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.Metadata;
using API.DTOs.Recommendation;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using API.Services.Plus;
using EasyCaching.Core;
using Kavita.Common.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

#nullable enable

public class MetadataController(IUnitOfWork unitOfWork, ILocalizationService localizationService, ILicenseService licenseService,
    IExternalMetadataService metadataService, IEasyCachingProviderFactory cachingProviderFactory)
    : BaseApiController
{
    private readonly IEasyCachingProvider _cacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusSeriesDetail);
    public const string CacheKey = "kavitaPlusSeriesDetail_";

    /// <summary>
    /// Fetches genres from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all genres</param>
    /// <returns></returns>
    [HttpGet("genres")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Instant, VaryByQueryKeys = new []{"libraryIds"})]
    public async Task<ActionResult<IList<GenreTagDto>>> GetAllGenres(string? libraryIds)
    {
        var ids = libraryIds?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await unitOfWork.GenreRepository.GetAllGenreDtosForLibrariesAsync(ids, User.GetUserId()));
        }

        return Ok(await unitOfWork.GenreRepository.GetAllGenreDtosAsync(User.GetUserId()));
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
            Ok(await unitOfWork.PersonRepository.GetAllPersonDtosByRoleAsync(User.GetUserId(), role!.Value)) :
            Ok(await unitOfWork.PersonRepository.GetAllPersonDtosAsync(User.GetUserId()));
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
        if (ids != null && ids.Count > 0)
        {
            return Ok(await unitOfWork.PersonRepository.GetAllPeopleDtosForLibrariesAsync(ids, User.GetUserId()));
        }
        return Ok(await unitOfWork.PersonRepository.GetAllPersonDtosAsync(User.GetUserId()));
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
        if (ids != null && ids.Count > 0)
        {
            return Ok(await unitOfWork.TagRepository.GetAllTagDtosForLibrariesAsync(ids, User.GetUserId()));
        }
        return Ok(await unitOfWork.TagRepository.GetAllTagDtosAsync(User.GetUserId()));
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
        if (ids != null && ids.Count > 0)
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
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute, VaryByQueryKeys = new [] {"libraryIds"})]
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
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.FiveMinute, VaryByQueryKeys = new []{"libraryIds"})]
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
    /// Returns summary for the chapter
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [HttpGet("chapter-summary")]
    public async Task<ActionResult<string>> GetChapterSummary(int chapterId)
    {
        if (chapterId <= 0) return BadRequest(await localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));
        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
        if (chapter == null) return BadRequest(await localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));
        return Ok(chapter.Summary);
    }

    /// <summary>
    /// Fetches the details needed from Kavita+ for Series Detail page
    /// </summary>
    /// <remarks>This will hit upstream K+ if the data in local db is 2 weeks old</remarks>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [HttpGet("series-detail-plus")]
    public async Task<ActionResult<SeriesDetailPlusDto>> GetKavitaPlusSeriesDetailData(int seriesId)
    {
        if (!await licenseService.HasActiveLicense())
        {
            return Ok(null);
        }

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(User.GetUserId());
        if (user == null) return Unauthorized();

        var userReviews = (await unitOfWork.UserRepository.GetUserRatingDtosForSeriesAsync(seriesId, user.Id))
            .Where(r => !string.IsNullOrEmpty(r.Body))
            .OrderByDescending(review => review.Username.Equals(user.UserName) ? 1 : 0)
            .ToList();

        var cacheKey = CacheKey + seriesId;
        var results = await _cacheProvider.GetAsync<SeriesDetailPlusDto>(cacheKey);
        if (results.HasValue)
        {
            var cachedResult = results.Value;
            await PrepareSeriesDetail(userReviews, cachedResult, user);
            return cachedResult;
        }

        var ret = await metadataService.GetSeriesDetail(seriesId);
        if (ret == null)
        {
            // Cache  an empty result, so we don't constantly hit K+ when we know nothing is going to resolve
            ret = new SeriesDetailPlusDto()
            {
                Reviews = new List<UserReviewDto>(),
                Recommendations = null,
                Ratings = null
            };
            await _cacheProvider.SetAsync(cacheKey, ret, TimeSpan.FromHours(48));

            var newCacheResult2 = (await _cacheProvider.GetAsync<SeriesDetailPlusDto>(cacheKey)).Value;
            await PrepareSeriesDetail(userReviews, newCacheResult2, user);

            return Ok(newCacheResult2);
        }

        await _cacheProvider.SetAsync(cacheKey, ret, TimeSpan.FromHours(48));

        // For some reason if we don't use a different instance, the cache keeps changes made below
        var newCacheResult = (await _cacheProvider.GetAsync<SeriesDetailPlusDto>(cacheKey)).Value;
        await PrepareSeriesDetail(userReviews, newCacheResult, user);

        return Ok(newCacheResult);

    }

    private async Task PrepareSeriesDetail(List<UserReviewDto> userReviews, SeriesDetailPlusDto ret, AppUser user)
    {
        var isAdmin = await unitOfWork.UserRepository.IsUserAdminAsync(user);
        userReviews.AddRange(ReviewService.SelectSpectrumOfReviews(ret.Reviews.ToList()));
        ret.Reviews = userReviews;

        if (ret.Recommendations != null)
        {
            ret.Recommendations.OwnedSeries ??= new List<SeriesDto>();
            await unitOfWork.SeriesRepository.AddSeriesModifiers(user.Id, ret.Recommendations.OwnedSeries);
        }

        if (!isAdmin && ret.Recommendations != null)
        {
            // Re-obtain owned series and take into account age restriction
            ret.Recommendations.OwnedSeries =
                await unitOfWork.SeriesRepository.GetSeriesDtoByIdsAsync(
                    ret.Recommendations.OwnedSeries.Select(s => s.Id), user);
            ret.Recommendations.ExternalSeries = new List<ExternalSeriesDto>();
        }
    }
}
