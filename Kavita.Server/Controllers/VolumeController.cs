using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.SignalR;
using Kavita.Server.Attributes;
using Kavita.Server.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

public class VolumeController(IUnitOfWork unitOfWork, ILocalizationService localizationService, IEventHub eventHub)
    : BaseApiController
{
    /// <summary>
    /// Returns the appropriate Volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [VolumeAccess]
    [HttpGet]
    public async Task<ActionResult<VolumeDto?>> GetVolume(int volumeId)
    {
        var ct = HttpContext.RequestAborted;
        return Ok(await unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, UserId, ct));
    }

    /// <summary>
    /// Updates the information on the Volume
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("update")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<VolumeDto>> UpdateVolume(UpdateVolumeDto dto)
    {
        var ct = HttpContext.RequestAborted;
        var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(dto.Id, ct: ct);
        if (volume == null) return BadRequest(await localizationService.TranslateAsync(UserId, "volume-doesnt-exist"));

        ExternalMetadataIdHelper.SetExternalMetadataIds(volume, dto);

        unitOfWork.VolumeRepository.Update(volume);

        if (unitOfWork.HasChanges() && !await unitOfWork.CommitAsync(ct))
            return BadRequest(await localizationService.TranslateAsync(UserId, "generic-error"));

        return Ok(await unitOfWork.VolumeRepository.GetVolumeDtoAsync(volume.Id, UserId, ct));
    }

    /// <summary>
    /// Delete the Volume from the DB
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [HttpDelete]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<bool>> DeleteVolume(int volumeId)
    {
        var ct = HttpContext.RequestAborted;
        var volume = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(volumeId,
            VolumeIncludes.Chapters | VolumeIncludes.People | VolumeIncludes.Tags, ct);
        if (volume == null)
            return BadRequest(await localizationService.TranslateAsync(UserId, "volume-doesnt-exist"));

        unitOfWork.VolumeRepository.Remove(volume);

        if (await unitOfWork.CommitAsync(ct))
        {
            await eventHub.SendMessageAsync(MessageFactory.VolumeRemoved, MessageFactory.VolumeRemovedEvent(volume.Id, volume.SeriesId), false, ct);
            return Ok(true);
        }

        return Ok(false);
    }

    /// <summary>
    /// Delete multiple Volumes from the DB
    /// </summary>
    /// <param name="volumesIds"></param>
    /// <returns></returns>
    [HttpPost("multiple")]
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    public async Task<ActionResult<bool>> DeleteMultipleVolumes(int[] volumesIds)
    {
        var ct = HttpContext.RequestAborted;
        var volumes = await unitOfWork.VolumeRepository.GetVolumesById(volumesIds, ct: ct);
        if (volumes.Count != volumesIds.Length)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, "volume-doesnt-exist"));
        }

        unitOfWork.VolumeRepository.Remove(volumes);

        if (!await unitOfWork.CommitAsync(ct))
        {
            return Ok(false);
        }

        foreach (var volume in volumes)
        {
            await eventHub.SendMessageAsync(MessageFactory.VolumeRemoved, MessageFactory.VolumeRemovedEvent(volume.Id, volume.SeriesId), false, ct);
        }

        return Ok(true);
    }
}
