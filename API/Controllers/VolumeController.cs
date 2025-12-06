using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Services;
using API.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;
#nullable enable

public class VolumeController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly IEventHub _eventHub;

    public VolumeController(IUnitOfWork unitOfWork, ILocalizationService localizationService, IEventHub eventHub)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _eventHub = eventHub;
    }

    /// <summary>
    /// Returns the appropriate Volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<VolumeDto?>> GetVolume(int volumeId)
    {
        return Ok(await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, UserId));
    }

    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpDelete]
    public async Task<ActionResult<bool>> DeleteVolume(int volumeId)
    {
        var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(volumeId,
            VolumeIncludes.Chapters | VolumeIncludes.People | VolumeIncludes.Tags);
        if (volume == null)
            return BadRequest(_localizationService.Translate(UserId, "volume-doesnt-exist"));

        _unitOfWork.VolumeRepository.Remove(volume);

        if (await _unitOfWork.CommitAsync())
        {
            await _eventHub.SendMessageAsync(MessageFactory.VolumeRemoved, MessageFactory.VolumeRemovedEvent(volume.Id, volume.SeriesId), false);
            return Ok(true);
        }

        return Ok(false);
    }

    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpPost("multiple")]
    public async Task<ActionResult<bool>> DeleteMultipleVolumes(int[] volumesIds)
    {
        var volumes = await _unitOfWork.VolumeRepository.GetVolumesById(volumesIds);
        if (volumes.Count != volumesIds.Length)
        {
            return BadRequest(_localizationService.Translate(UserId, "volume-doesnt-exist"));
        }

        _unitOfWork.VolumeRepository.Remove(volumes);

        if (!await _unitOfWork.CommitAsync())
        {
            return Ok(false);
        }

        foreach (var volume in volumes)
        {
            await _eventHub.SendMessageAsync(MessageFactory.VolumeRemoved, MessageFactory.VolumeRemovedEvent(volume.Id, volume.SeriesId), false);
        }

        return Ok(true);
    }
}
