﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.Metadata;
using API.Entities.Enums;
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
    public async Task<ActionResult<IList<GenreTagDto>>> GetAllGenres(string? libraryIds)
    {
        var ids = libraryIds?.Split(",").Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.GenreRepository.GetAllGenreDtosForLibrariesAsync(ids));
        }

        return Ok(await _unitOfWork.GenreRepository.GetAllGenreDtosAsync());
    }

    /// <summary>
    /// Fetches people from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all people</param>
    /// <returns></returns>
    [HttpGet("people")]
    public async Task<ActionResult<IList<PersonDto>>> GetAllPeople(string? libraryIds)
    {
        var ids = libraryIds?.Split(",").Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.PersonRepository.GetAllPeopleDtosForLibrariesAsync(ids));
        }
        return Ok(await _unitOfWork.PersonRepository.GetAllPeople());
    }

    /// <summary>
    /// Fetches all tags from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all tags</param>
    /// <returns></returns>
    [HttpGet("tags")]
    public async Task<ActionResult<IList<TagDto>>> GetAllTags(string? libraryIds)
    {
        var ids = libraryIds?.Split(",").Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.TagRepository.GetAllTagDtosForLibrariesAsync(ids));
        }
        return Ok(await _unitOfWork.TagRepository.GetAllTagDtosAsync());
    }

    /// <summary>
    /// Fetches all age ratings from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all ratings</param>
    /// <returns></returns>
    [HttpGet("age-ratings")]
    public async Task<ActionResult<IList<AgeRatingDto>>> GetAllAgeRatings(string? libraryIds)
    {
        var ids = libraryIds?.Split(",").Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.SeriesRepository.GetAllAgeRatingsDtosForLibrariesAsync(ids));
        }

        return Ok(Enum.GetValues<AgeRating>().Select(t => new AgeRatingDto()
        {
            Title = t.ToDescription(),
            Value = t
        }));
    }

    /// <summary>
    /// Fetches all publication status' from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all publication status</param>
    /// <returns></returns>
    [HttpGet("publication-status")]
    public async Task<ActionResult<IList<AgeRatingDto>>> GetAllPublicationStatus(string? libraryIds)
    {
        var ids = libraryIds?.Split(",").Select(int.Parse).ToList();
        if (ids != null && ids.Count > 0)
        {
            return Ok(await _unitOfWork.SeriesRepository.GetAllPublicationStatusesDtosForLibrariesAsync(ids));
        }

        return Ok(Enum.GetValues<PublicationStatus>().Select(t => new PublicationStatusDto()
        {
            Title = t.ToDescription(),
            Value = t
        }).OrderBy(t => t.Title));
    }

    /// <summary>
    /// Fetches all age ratings from the instance
    /// </summary>
    /// <param name="libraryIds">String separated libraryIds or null for all ratings</param>
    /// <returns></returns>
    [HttpGet("languages")]
    public async Task<ActionResult<IList<LanguageDto>>> GetAllLanguages(string? libraryIds)
    {
        var ids = libraryIds?.Split(",").Select(int.Parse).ToList();
        if (ids is {Count: > 0})
        {
            return Ok(await _unitOfWork.SeriesRepository.GetAllLanguagesForLibrariesAsync(ids));
        }

        var englishTag = CultureInfo.GetCultureInfo("en");
        return Ok(new List<LanguageDto>()
        {
            new ()
            {
                Title = englishTag.DisplayName,
                IsoCode = englishTag.IetfLanguageTag
            }
        });
    }

    [HttpGet("all-languages")]
    public IEnumerable<LanguageDto> GetAllValidLanguages()
    {
        return CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c =>
            new LanguageDto()
            {
                Title = c.DisplayName,
                IsoCode = c.IetfLanguageTag
            }).Where(l => !string.IsNullOrEmpty(l.IsoCode));
    }
}
