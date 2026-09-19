using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Models;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.User;
using Kavita.Server.Attributes;
using Kavita.Services.Helpers.SmartFilter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

public class FilterController(
    IUnitOfWork unitOfWork,
    ILocalizationService localizationService,
    IStreamService streamService,
    ILogger<FilterController> logger,
    IEventHub eventHub)
    : BaseApiController
{
    /// <summary>
    /// Creates or Updates the Series filter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update/series")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> CreateOrUpdateSeriesSmartFilter(SeriesFilterV2Dto dto)
    {
        var ct = HttpContext.RequestAborted;
        try
        {
            if (string.IsNullOrEmpty(dto.Name)) return BadRequest("Name is required");
            var encodedString = SmartFilterHelper.Encode(dto);
            await ValidateAndSaveFilterUpsert(dto.Name!, encodedString, dto.EntityType, ct);
            return Ok();
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Creates or Updates the Reading List filter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update/reading-list")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> CreateOrUpdateReadingListSmartFilter(ReadingListFilterDto dto)
    {
        var ct = HttpContext.RequestAborted;
        try
        {
            if (string.IsNullOrEmpty(dto.Name)) return BadRequest("Name is required");
            var encodedString = SmartFilterHelper.Encode(dto);
            await ValidateAndSaveFilterUpsert(dto.Name!, encodedString, dto.EntityType, ct);
            return Ok();
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Creates or Updates the Person filter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update/person")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> CreateOrUpdatePersonSmartFilter(PersonFilterDto dto)
    {
        var ct = HttpContext.RequestAborted;
        try
        {
            if (string.IsNullOrEmpty(dto.Name)) return BadRequest("Name is required");
            var encodedString = SmartFilterHelper.Encode(dto);
            await ValidateAndSaveFilterUpsert(dto.Name!, encodedString, dto.EntityType, ct);
            return Ok();
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Creates or Updates the Reading List filter
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update/annotation")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> CreateOrUpdateAnnotationSmartFilter(AnnotationFilterDto dto)
    {
        var ct = HttpContext.RequestAborted;
        try
        {
            if (string.IsNullOrEmpty(dto.Name)) return BadRequest("Name is required");
            var encodedString = SmartFilterHelper.Encode(dto);
            await ValidateAndSaveFilterUpsert(dto.Name!, encodedString, dto.EntityType, ct);
            return Ok();
        }
        catch (KavitaException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task ValidateAndSaveFilterUpsert(string filterName, string encodedFilter,  FilterEntityType entityType, CancellationToken ct = default)
    {
        var user = (await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.SmartFilters, ct))!;

        if (string.IsNullOrWhiteSpace(filterName)) throw new KavitaException("Name must be set");
        if (Defaults.DefaultStreams.Any(s => s.Name.Equals(filterName, StringComparison.InvariantCultureIgnoreCase)))
        {
            // NOTE: This checks against localization keys (on-deck), so this case will almost never happen
            throw new KavitaException("You cannot use the name of a system provided stream");
        }

        var existingFilter = user.SmartFilters.FirstOrDefault(s => s.Name.Equals(filterName, StringComparison.InvariantCultureIgnoreCase));
        if (existingFilter != null)
        {
            // Update the filter
            existingFilter.Filter = encodedFilter;
            unitOfWork.AppUserSmartFilterRepository.Update(existingFilter);
        }
        else
        {
            existingFilter = new AppUserSmartFilter()
            {
                Name = filterName,
                Filter = encodedFilter,
                EntityType = entityType
            };
            user.SmartFilters.Add(existingFilter);
            unitOfWork.UserRepository.Update(user);
        }

        if (!unitOfWork.HasChanges()) return;
        await unitOfWork.CommitAsync(ct);
    }


    /// <summary>
    /// All Smart Filters for the authenticated user
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SmartFilterDto>>> GetFilters()
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.AppUserSmartFilterRepository.GetAllDtosByUserId(UserId, ct));
    }

    /// <summary>
    /// Delete the smart filter for the authenticated user
    /// </summary>
    /// <remarks>User must not be in <see cref="PolicyConstants.ReadOnlyRole"/></remarks>
    /// <param name="filterId"></param>
    /// <returns></returns>
    [HttpDelete]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteFilter(int filterId)
    {
        var ct = HttpContext.RequestAborted;
        var filter = await unitOfWork.AppUserSmartFilterRepository.GetById(filterId, ct);
        if (filter == null) return Ok();

        if (filter.AppUserId != UserId)
        {
            return NotFound();
        }

        // This needs to delete any dashboard filters that have it too
        var streams = await unitOfWork.UserRepository.GetDashboardStreamWithFilter(filter.Id, ct);
        unitOfWork.UserRepository.Delete(streams);

        var streams2 = await unitOfWork.UserRepository.GetSideNavStreamWithFilter(filter.Id, ct);
        unitOfWork.UserRepository.Delete(streams2);

        unitOfWork.AppUserSmartFilterRepository.Delete(filter);
        await unitOfWork.CommitAsync(ct);

        await eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(UserId),
            UserId, ct);

        return Ok();
    }

    /// <summary>
    /// Encode a Series filter
    /// </summary>
    /// <param name="dto">This must be entityType Series</param>
    /// <returns></returns>
    [HttpPost("encode/series")]
    public ActionResult<string> EncodeSeriesFilter(SeriesFilterV2Dto dto)
    {
        return Ok(SmartFilterHelper.Encode(dto));
    }

    /// <summary>
    /// Encode a Reading List filter
    /// </summary>
    /// <param name="dto">This must be entityType ReadingList</param>
    /// <returns></returns>
    [HttpPost("encode/reading-list")]
    public ActionResult<string> EncodeRlFilter(ReadingListFilterDto dto)
    {
        return Ok(SmartFilterHelper.Encode(dto));
    }

    /// <summary>
    /// Encode a Person Filter
    /// </summary>
    /// <param name="dto">This must be entityType Person</param>
    /// <returns></returns>
    [HttpPost("encode/person")]
    public ActionResult<string> EncodePersonFilter(PersonFilterDto dto)
    {
        return Ok(SmartFilterHelper.Encode(dto));
    }

    /// <summary>
    /// Encode an Annotation Filter
    /// </summary>
    /// <param name="dto">This must be entityType Annotation</param>
    /// <returns></returns>
    [HttpPost("encode/annotation")]
    public ActionResult<string> EncodeAnnotationFilter(AnnotationFilterDto dto)
    {
        return Ok(SmartFilterHelper.Encode(dto));
    }

    /// <summary>
    /// Decodes the Filter
    /// </summary>
    /// <remarks>Decoded filter will always have the same shape of <see cref="IFilterDto{TStatement,TSortOption}"/>.
    /// The concrete class is driven by <c>EntityType</c>.
    /// Classes: <see cref="SeriesFilterV2Dto"/>, <see cref="PersonFilterDto"/>, <see cref="AnnotationFilterDto"/>, <see cref="ReadingListFilterDto"/>
    /// </remarks>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("decode")]
    public ActionResult<IFilterDto> DecodeFilter(DecodeFilterDto dto)
    {
        return Ok(SmartFilterHelper.Decode(dto.EncodedFilter));
    }

    /// <summary>
    /// Rename a Smart Filter given the filterId and new name
    /// </summary>
    /// <param name="filterId"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    [HttpPost("rename")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> RenameFilter([FromQuery] int filterId, [FromQuery] [Required] string name)
    {
        var ct = HttpContext.RequestAborted;
        try
        {
            var user = await unitOfWork.UserRepository.GetUserByIdAsync(UserId, AppUserIncludes.SmartFilters, ct);
            if (user == null) return Unauthorized();

            name = name.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(await localizationService.TranslateAsync(user.Id, "smart-filter-name-required"));
            }

            if (Defaults.DefaultStreams.Any(s => s.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
            {
                return BadRequest(await localizationService.TranslateAsync(user.Id, "smart-filter-system-name"));
            }

            var filter = user.SmartFilters.FirstOrDefault(f => f.Id == filterId);
            if (filter == null)
            {
                return BadRequest(await localizationService.TranslateAsync(user.Id, "filter-not-found"));
            }

            filter.Name = name;
            unitOfWork.AppUserSmartFilterRepository.Update(filter);
            await unitOfWork.CommitAsync(ct);

            await streamService.RenameSmartFilterStreams(filter, ct);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when renaming smart filter: {FilterId}", filterId);
            return BadRequest(await localizationService.TranslateAsync(UserId, "generic-error"));
        }

    }
}
