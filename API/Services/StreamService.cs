using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Dashboard;
using API.DTOs.SideNav;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.SignalR;
using Kavita.Common;
using Kavita.Common.Helpers;

namespace API.Services;

/// <summary>
/// For SideNavStream and DashboardStream manipulation
/// </summary>
public interface IStreamService
{
    Task<IEnumerable<DashboardStreamDto>> GetDashboardStreams(int userId, bool visibleOnly = true);
    Task<IEnumerable<SideNavStreamDto>> GetSidenavStreams(int userId, bool visibleOnly = true);
    Task<IEnumerable<ExternalSourceDto>> GetExternalSources(int userId);
    Task<DashboardStreamDto> CreateDashboardStreamFromSmartFilter(int userId, int smartFilterId);
    Task UpdateDashboardStream(int userId, DashboardStreamDto dto);
    Task UpdateDashboardStreamPosition(int userId, UpdateStreamPositionDto dto);
    Task UpdateSideNavStreamBulk(int userId, BulkUpdateSideNavStreamVisibilityDto dto);
    Task<SideNavStreamDto> CreateSideNavStreamFromSmartFilter(int userId, int smartFilterId);
    Task<SideNavStreamDto> CreateSideNavStreamFromExternalSource(int userId, int externalSourceId);
    Task UpdateSideNavStream(int userId, SideNavStreamDto dto);
    Task UpdateSideNavStreamPosition(int userId, UpdateStreamPositionDto dto);
    Task<ExternalSourceDto> CreateExternalSource(int userId, ExternalSourceDto dto);
    Task<ExternalSourceDto> UpdateExternalSource(int userId, ExternalSourceDto dto);
    Task DeleteExternalSource(int userId, int externalSourceId);
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

    public async Task<IEnumerable<ExternalSourceDto>> GetExternalSources(int userId)
    {
        return await _unitOfWork.AppUserExternalSourceRepository.GetExternalSources(userId);
    }

    public async Task<DashboardStreamDto> CreateDashboardStreamFromSmartFilter(int userId, int smartFilterId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.DashboardStreams);
        if (user == null) throw new KavitaException(await _localizationService.Translate(userId, "no-user"));

        var smartFilter = await _unitOfWork.AppUserSmartFilterRepository.GetById(smartFilterId);
        if (smartFilter == null) throw new KavitaException(await _localizationService.Translate(userId, "smart-filter-doesnt-exist"));

        var stream = user.DashboardStreams.FirstOrDefault(d => d.SmartFilter?.Id == smartFilterId);
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
        if (stream.Order == dto.ToPosition) return;

        var list = user!.DashboardStreams.OrderBy(s => s.Order).ToList();
        OrderableHelper.ReorderItems(list, stream.Id, dto.ToPosition);
        user.DashboardStreams = list;

        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();
        if (!stream.Visible) return;
        await _eventHub.SendMessageToAsync(MessageFactory.DashboardUpdate, MessageFactory.DashboardUpdateEvent(user.Id),
            user.Id);
    }

    public async Task UpdateSideNavStreamBulk(int userId, BulkUpdateSideNavStreamVisibilityDto dto)
    {
        var streams = await _unitOfWork.UserRepository.GetDashboardStreamsByIds(dto.Ids);
        foreach (var stream in streams)
        {
            stream.Visible = dto.Visibility;
            _unitOfWork.UserRepository.Update(stream);
        }

        await _unitOfWork.CommitAsync();
        await _eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(userId),
            userId);
    }

    public async Task<SideNavStreamDto> CreateSideNavStreamFromSmartFilter(int userId, int smartFilterId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.SideNavStreams);
        if (user == null) throw new KavitaException(await _localizationService.Translate(userId, "no-user"));

        var smartFilter = await _unitOfWork.AppUserSmartFilterRepository.GetById(smartFilterId);
        if (smartFilter == null) throw new KavitaException(await _localizationService.Translate(userId, "smart-filter-doesnt-exist"));

        var stream = user.SideNavStreams.FirstOrDefault(d => d.SmartFilter?.Id == smartFilterId);
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

    public async Task<SideNavStreamDto> CreateSideNavStreamFromExternalSource(int userId, int externalSourceId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.SideNavStreams);
        if (user == null) throw new KavitaException(await _localizationService.Translate(userId, "no-user"));

        var externalSource = await _unitOfWork.AppUserExternalSourceRepository.GetById(externalSourceId);
        if (externalSource == null) throw new KavitaException(await _localizationService.Translate(userId, "external-source-doesnt-exist"));

        var stream = user?.SideNavStreams.FirstOrDefault(d => d.ExternalSourceId == externalSourceId);
        if (stream != null) throw new KavitaException(await _localizationService.Translate(userId, "external-source-already-in-use"));

        var maxOrder = user!.SideNavStreams.Max(d => d.Order);
        var createdStream = new AppUserSideNavStream()
        {
            Name = externalSource.Name,
            IsProvided = false,
            StreamType = SideNavStreamType.ExternalSource,
            Visible = true,
            Order = maxOrder + 1,
            ExternalSourceId = externalSource.Id
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
            StreamType = createdStream.StreamType,
            ExternalSource = new ExternalSourceDto()
            {
                Host = externalSource.Host,
                Id = externalSource.Id,
                Name = externalSource.Name,
                ApiKey = externalSource.ApiKey
            }
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

        var list = user!.SideNavStreams.OrderBy(s => s.Order).ToList();
        OrderableHelper.ReorderItems(list, stream.Id, dto.ToPosition);
        user.SideNavStreams = list;

        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();
        if (!stream.Visible) return;
        await _eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(userId),
            userId);
    }

    public async Task<ExternalSourceDto> CreateExternalSource(int userId, ExternalSourceDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId,
            AppUserIncludes.ExternalSources);
        if (user == null) throw new KavitaException("not-authenticated");

        if (user.ExternalSources.Any(s => s.Host == dto.Host))
        {
            throw new KavitaException("external-source-already-exists");
        }

        if (string.IsNullOrEmpty(dto.ApiKey) || string.IsNullOrEmpty(dto.Name)) throw new KavitaException("external-source-required");
        if (!UrlHelper.StartsWithHttpOrHttps(dto.Host)) throw new KavitaException("external-source-host-format");


        var newSource = new AppUserExternalSource()
        {
            Name = dto.Name,
            Host = UrlHelper.EnsureEndsWithSlash(
                UrlHelper.EnsureStartsWithHttpOrHttps(dto.Host)),
            ApiKey = dto.ApiKey
        };
        user.ExternalSources.Add(newSource);

        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        dto.Id = newSource.Id;

        return dto;
    }

    public async Task<ExternalSourceDto> UpdateExternalSource(int userId, ExternalSourceDto dto)
    {
        var source = await _unitOfWork.AppUserExternalSourceRepository.GetById(dto.Id);
        if (source == null) throw new KavitaException("external-source-doesnt-exist");
        if (source.AppUserId != userId) throw new KavitaException("external-source-doesnt-exist");

        if (string.IsNullOrEmpty(dto.ApiKey) || string.IsNullOrEmpty(dto.Host) || string.IsNullOrEmpty(dto.Name)) throw new KavitaException("external-source-required");

        source.Host = UrlHelper.EnsureEndsWithSlash(
            UrlHelper.EnsureStartsWithHttpOrHttps(dto.Host));
        source.ApiKey = dto.ApiKey;
        source.Name = dto.Name;

        _unitOfWork.AppUserExternalSourceRepository.Update(source);
        await _unitOfWork.CommitAsync();

        dto.Host = source.Host;
        return dto;
    }

    public async Task DeleteExternalSource(int userId, int externalSourceId)
    {
        var source = await _unitOfWork.AppUserExternalSourceRepository.GetById(externalSourceId);
        if (source == null) throw new KavitaException("external-source-doesnt-exist");
        if (source.AppUserId != userId) throw new KavitaException("external-source-doesnt-exist");

        _unitOfWork.AppUserExternalSourceRepository.Delete(source);

        // Find all SideNav's with this source and delete them as well
        var streams2 = await _unitOfWork.UserRepository.GetSideNavStreamWithExternalSource(externalSourceId);
        _unitOfWork.UserRepository.Delete(streams2);

        await _unitOfWork.CommitAsync();
    }
}
