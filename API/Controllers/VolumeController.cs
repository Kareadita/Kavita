using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Extensions;
using API.Services;
using API.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

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

    [HttpGet]
    public async Task<ActionResult<VolumeDto>> GetVolume(int volumeId)
    {
        var volume =
            await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, User.GetUserId());

        return Ok(volume);
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete]
    public async Task<ActionResult<bool>> DeleteVolume(int volumeId)
    {
        var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(volumeId,
            VolumeIncludes.Chapters | VolumeIncludes.People | VolumeIncludes.Tags);
        if (volume == null)
            return BadRequest(_localizationService.Translate(User.GetUserId(), "chapter-doesnt-exist"));

        _unitOfWork.VolumeRepository.Remove(volume);

        if (await _unitOfWork.CommitAsync())
        {
            await _eventHub.SendMessageAsync(MessageFactory.VolumeRemoved, MessageFactory.VolumeRemovedEvent(volume.Id, volume.SeriesId), false);
            return Ok(true);
        }

        return Ok(false);
    }
}
