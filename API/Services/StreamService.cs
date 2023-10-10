using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Dashboard;
using API.DTOs.SideNav;
using API.Entities;
using API.Entities.Enums;
using API.SignalR;
using Kavita.Common;

namespace API.Services;

/// <summary>
/// For SideNavStream and DashboardStream manipulation
/// </summary>
public interface IStreamService
{
    Task<IEnumerable<DashboardStreamDto>> GetDashboardStreams(int userId, bool visibleOnly = true);
    Task<IEnumerable<SideNavStreamDto>> GetSidenavStreams(int userId, bool visibleOnly = true);
    Task<DashboardStreamDto> CreateDashboardStreamFromSmartFilter(int userId, int smartFilterId);
    Task UpdateDashboardStream(int userId, DashboardStreamDto dto);
    Task UpdateDashboardStreamPosition(int userId, UpdateStreamPositionDto dto);
    Task<SideNavStreamDto> CreateSideNavStreamFromSmartFilter(int userId, int smartFilterId);
    Task UpdateSideNavStream(int userId, SideNavStreamDto dto);
    Task UpdateSideNavStreamPosition(int userId, UpdateStreamPositionDto dto);
}

public class StreamService : IStreamService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly ILocalizationService _localizationService;

    public StreamService(IUnitOfWork unitOfWork, IEventHub eventHub, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _localizationService = localizationService;
    }

    public async Task<IEnumerable<DashboardStreamDto>> GetDashboardStreams(int userId, bool visibleOnly = true)
    {
        return await _unitOfWork.UserRepository.GetDashboardStreams(userId, visibleOnly);
    }

    public async Task<IEnumerable<SideNavStreamDto>> GetSidenavStreams(int userId, bool visibleOnly = true)
    {
        return await _unitOfWork.UserRepository.GetSideNavStreams(userId, visibleOnly);
    }

    public async Task<DashboardStreamDto> CreateDashboardStreamFromSmartFilter(int userId, int smartFilterId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.DashboardStreams);
        if (user == null) throw new KavitaException(await _localizationService.Translate(userId, "no-user"));

        var smartFilter = await _unitOfWork.AppUserSmartFilterRepository.GetById(smartFilterId);
        if (smartFilter == null) throw new KavitaException(await _localizationService.Translate(userId, "smart-filter-doesnt-exist"));

        var stream = user?.DashboardStreams.FirstOrDefault(d => d.SmartFilter?.Id == smartFilterId);
        if (stream != null) throw new KavitaException(await _localizationService.Translate(userId, "smart-filter-already-in-use"));

        var maxOrder = user!.DashboardStreams.Max(d => d.Order);
        var createdStream = new AppUserDashboardStream()
        {
            Name = smartFilter.Name,
            IsProvided = false,
            StreamType = DashboardStreamType.SmartFilter,
            Visible = true,
            Order = maxOrder + 1,
            SmartFilter = smartFilter
        };

        user.DashboardStreams.Add(createdStream);
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        var ret = new DashboardStreamDto()
        {
            Name = createdStream.Name,
            IsProvided = createdStream.IsProvided,
            Visible = createdStream.Visible,
            Order = createdStream.Order,
            SmartFilterEncoded = smartFilter.Filter,
            StreamType = createdStream.StreamType
        };

        await _eventHub.SendMessageToAsync(MessageFactory.DashboardUpdate, MessageFactory.DashboardUpdateEvent(user.Id),
            userId);

        return ret;
    }

    public async Task UpdateDashboardStream(int userId, DashboardStreamDto dto)
    {
        var stream = await _unitOfWork.UserRepository.GetDashboardStream(dto.Id);
        if (stream == null) throw new KavitaException(await _localizationService.Translate(userId, "dashboard-stream-doesnt-exist"));
        stream.Visible = dto.Visible;

        _unitOfWork.UserRepository.Update(stream);
        await _unitOfWork.CommitAsync();
        await _eventHub.SendMessageToAsync(MessageFactory.DashboardUpdate, MessageFactory.DashboardUpdateEvent(userId),
            userId);
    }

    public async Task UpdateDashboardStreamPosition(int userId, UpdateStreamPositionDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId,
            AppUserIncludes.DashboardStreams);
        var stream = user?.DashboardStreams.FirstOrDefault(d => d.Id == dto.Id);
        if (stream == null)
            throw new KavitaException(await _localizationService.Translate(userId, "dashboard-stream-doesnt-exist"));
        if (stream.Order == dto.ToPosition) return ;

        var list = user!.DashboardStreams.ToList();
        ReorderItems(list, stream.Id, dto.ToPosition);
        user.DashboardStreams = list;

        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();
        await _eventHub.SendMessageToAsync(MessageFactory.DashboardUpdate, MessageFactory.DashboardUpdateEvent(user.Id),
            user.Id);
    }

    public async Task<SideNavStreamDto> CreateSideNavStreamFromSmartFilter(int userId, int smartFilterId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.SideNavStreams);
        if (user == null) throw new KavitaException(await _localizationService.Translate(userId, "no-user"));

        var smartFilter = await _unitOfWork.AppUserSmartFilterRepository.GetById(smartFilterId);
        if (smartFilter == null) throw new KavitaException(await _localizationService.Translate(userId, "smart-filter-doesnt-exist"));

        var stream = user?.SideNavStreams.FirstOrDefault(d => d.SmartFilter?.Id == smartFilterId);
        if (stream != null) throw new KavitaException(await _localizationService.Translate(userId, "smart-filter-already-in-use"));

        var maxOrder = user!.SideNavStreams.Max(d => d.Order);
        var createdStream = new AppUserSideNavStream()
        {
            Name = smartFilter.Name,
            IsProvided = false,
            StreamType = SideNavStreamType.SmartFilter,
            Visible = true,
            Order = maxOrder + 1,
            SmartFilter = smartFilter
        };

        user.SideNavStreams.Add(createdStream);
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        var ret = new SideNavStreamDto()
        {
            Name = createdStream.Name,
            IsProvided = createdStream.IsProvided,
            Visible = createdStream.Visible,
            Order = createdStream.Order,
            SmartFilterEncoded = smartFilter.Filter,
            StreamType = createdStream.StreamType
        };


        await _eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(userId),
            userId);
        return ret;
    }

    public async Task UpdateSideNavStream(int userId, SideNavStreamDto dto)
    {
        var stream = await _unitOfWork.UserRepository.GetSideNavStream(dto.Id);
        if (stream == null)
            throw new KavitaException(await _localizationService.Translate(userId, "sidenav-stream-doesnt-exist"));
        stream.Visible = dto.Visible;

        _unitOfWork.UserRepository.Update(stream);
        await _unitOfWork.CommitAsync();
        await _eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(userId),
            userId);
    }

    public async Task UpdateSideNavStreamPosition(int userId, UpdateStreamPositionDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId,
            AppUserIncludes.SideNavStreams);
        var stream = user?.SideNavStreams.FirstOrDefault(d => d.Id == dto.Id);
        if (stream == null) throw new KavitaException(await _localizationService.Translate(userId, "sidenav-stream-doesnt-exist"));
        if (stream.Order == dto.ToPosition) return;

        var list = user!.SideNavStreams.ToList();
        ReorderItems(list, stream.Id, dto.ToPosition);
        user.SideNavStreams = list;

        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();
        await _eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(userId),
            userId);
    }

    private static void ReorderItems(List<AppUserDashboardStream> items, int itemId, int toPosition)
    {
        var item = items.Find(r => r.Id == itemId);
        if (item != null)
        {
            items.Remove(item);
            items.Insert(toPosition, item);
        }

        for (var i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
        }
    }

    private static void ReorderItems(List<AppUserSideNavStream> items, int itemId, int toPosition)
    {
        var item = items.Find(r => r.Id == itemId);
        if (item != null)
        {
            items.Remove(item);
            items.Insert(toPosition, item);
        }

        for (var i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
        }
    }
}
