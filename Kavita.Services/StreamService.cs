using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.SideNav;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Models.Helpers;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public class StreamService(
    IUnitOfWork unitOfWork,
    IEventHub eventHub,
    ILocalizationService localizationService,
    ILogger<StreamService> logger)
    : IStreamService
{
    public async Task<IEnumerable<DashboardStreamDto>> GetDashboardStreams(int userId, bool visibleOnly = true,
        CancellationToken ct = default)
    {
        return await unitOfWork.UserRepository.GetDashboardStreams(userId, visibleOnly, ct);
    }

    public async Task<IEnumerable<SideNavStreamDto>> GetSidenavStreams(int userId, bool visibleOnly = true, CancellationToken ct = default)
    {
        return await unitOfWork.UserRepository.GetSideNavStreams(userId, visibleOnly, ct);
    }

    public async Task<IEnumerable<ExternalSourceDto>> GetExternalSources(int userId, CancellationToken ct = default)
    {
        return await unitOfWork.AppUserExternalSourceRepository.GetExternalSources(userId, ct);
    }

    public async Task<DashboardStreamDto> CreateDashboardStreamFromSmartFilter(int userId, int smartFilterId,
        CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.DashboardStreams, ct);
        if (user == null) throw new KavitaException(await localizationService.Translate(userId, "no-user"));

        var smartFilter = await unitOfWork.AppUserSmartFilterRepository.GetById(smartFilterId, ct);
        if (smartFilter == null) throw new KavitaException(await localizationService.Translate(userId, "smart-filter-doesnt-exist"));

        var stream = user.DashboardStreams.FirstOrDefault(d => d.SmartFilter?.Id == smartFilterId);
        if (stream != null) throw new KavitaException(await localizationService.Translate(userId, "smart-filter-already-in-use"));

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
        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync(ct);

        var ret = new DashboardStreamDto()
        {
            Id = createdStream.Id,
            Name = createdStream.Name,
            IsProvided = createdStream.IsProvided,
            Visible = createdStream.Visible,
            Order = createdStream.Order,
            SmartFilterEncoded = smartFilter.Filter,
            StreamType = createdStream.StreamType
        };

        await eventHub.SendMessageToAsync(MessageFactory.DashboardUpdate, MessageFactory.DashboardUpdateEvent(user.Id),
            userId, ct);

        return ret;
    }

    public async Task UpdateDashboardStream(int userId, DashboardStreamDto dto, CancellationToken ct = default)
    {
        var stream = await unitOfWork.UserRepository.GetDashboardStream(dto.Id, ct);
        if (stream == null) throw new KavitaException(await localizationService.Translate(userId, "dashboard-stream-doesnt-exist"));
        stream.Visible = dto.Visible;

        unitOfWork.UserRepository.Update(stream);
        await unitOfWork.CommitAsync(ct);
        await eventHub.SendMessageToAsync(MessageFactory.DashboardUpdate, MessageFactory.DashboardUpdateEvent(userId),
            userId, ct);
    }

    public async Task UpdateDashboardStreamPosition(int userId, UpdateStreamPositionDto dto,
        CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId,
            AppUserIncludes.DashboardStreams, ct);
        var stream = user?.DashboardStreams.FirstOrDefault(d => d.Id == dto.Id);
        if (stream == null)
        {
            throw new KavitaException(await localizationService.Translate(userId, "dashboard-stream-doesnt-exist"));
        }

        if (stream.Order == dto.ToPosition) return;

        var list = user!.DashboardStreams.OrderBy(s => s.Order).ToList();
        OrderableHelper.ReorderItems(list, stream.Id, dto.ToPosition);
        user.DashboardStreams = list;

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync(ct);
        if (!stream.Visible) return;
        await eventHub.SendMessageToAsync(MessageFactory.DashboardUpdate, MessageFactory.DashboardUpdateEvent(user.Id),
            user.Id, ct);
    }

    public async Task UpdateSideNavStreamBulk(int userId, BulkUpdateSideNavStreamVisibilityDto dto,
        CancellationToken ct = default)
    {
        var streams = await unitOfWork.UserRepository.GetDashboardStreamsByIds(dto.Ids, ct);
        foreach (var stream in streams)
        {
            stream.Visible = dto.Visibility;
            unitOfWork.UserRepository.Update(stream);
        }

        await unitOfWork.CommitAsync(ct);
        await eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(userId),
            userId, ct);
    }

    public async Task<SideNavStreamDto> CreateSideNavStreamFromSmartFilter(int userId, int smartFilterId,
        CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.SideNavStreams, ct);
        if (user == null) throw new KavitaException(await localizationService.Translate(userId, "no-user"));

        var smartFilter = await unitOfWork.AppUserSmartFilterRepository.GetById(smartFilterId, ct);
        if (smartFilter == null) throw new KavitaException(await localizationService.Translate(userId, "smart-filter-doesnt-exist"));

        var stream = user.SideNavStreams.FirstOrDefault(d => d.SmartFilter?.Id == smartFilterId);
        if (stream != null) throw new KavitaException(await localizationService.Translate(userId, "smart-filter-already-in-use"));

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
        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync(ct);

        var ret = new SideNavStreamDto()
        {
            Id = createdStream.Id,
            Name = createdStream.Name,
            IsProvided = createdStream.IsProvided,
            Visible = createdStream.Visible,
            Order = createdStream.Order,
            SmartFilterEncoded = smartFilter.Filter,
            StreamType = createdStream.StreamType
        };


        await eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(userId),
            userId, ct);
        return ret;
    }

    public async Task<SideNavStreamDto> CreateSideNavStreamFromExternalSource(int userId, int externalSourceId,
        CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.SideNavStreams, ct);
        if (user == null) throw new KavitaException(await localizationService.Translate(userId, "no-user"));

        var externalSource = await unitOfWork.AppUserExternalSourceRepository.GetById(externalSourceId, ct);
        if (externalSource == null) throw new KavitaException(await localizationService.Translate(userId, "external-source-doesnt-exist"));

        var stream = user?.SideNavStreams.FirstOrDefault(d => d.ExternalSourceId == externalSourceId);
        if (stream != null) throw new KavitaException(await localizationService.Translate(userId, "external-source-already-in-use"));

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
        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync(ct);

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


        await eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(userId),
            userId, ct);
        return ret;
    }

    public async Task UpdateSideNavStream(int userId, SideNavStreamDto dto, CancellationToken ct = default)
    {
        var stream = await unitOfWork.UserRepository.GetSideNavStream(dto.Id, ct);
        if (stream == null)
            throw new KavitaException(await localizationService.Translate(userId, "sidenav-stream-doesnt-exist"));

        stream.Visible = dto.Visible;

        unitOfWork.UserRepository.Update(stream);
        await unitOfWork.CommitAsync(ct);
        await eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(userId),
            userId, ct);
    }

    public async Task UpdateSideNavStreamPosition(int userId, UpdateStreamPositionDto dto, CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId,
            AppUserIncludes.SideNavStreams, ct);
        var stream = user?.SideNavStreams.FirstOrDefault(d => d.Id == dto.Id);
        if (stream == null) throw new KavitaException(await localizationService.Translate(userId, "sidenav-stream-doesnt-exist"));

        if (stream.Order == dto.ToPosition) return;

        var list = user!.SideNavStreams.OrderBy(s => s.Order).ToList();

        var wantedPosition = dto.ToPosition;
        if (!dto.PositionIncludesInvisible)
        {
            var visibleItems = list.Where(i => i.Visible).ToList();
            if (dto.ToPosition < 0 || dto.ToPosition >= visibleItems.Count) return;

            var itemAtWantedPosition = visibleItems[dto.ToPosition];
            wantedPosition = list.IndexOf(itemAtWantedPosition);
        }

        OrderableHelper.ReorderItems(list, stream.Id, wantedPosition);
        user.SideNavStreams = list;

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync(ct);
        if (!stream.Visible) return;
        await eventHub.SendMessageToAsync(MessageFactory.SideNavUpdate, MessageFactory.SideNavUpdateEvent(userId),
            userId, ct);
    }

    public async Task<ExternalSourceDto> CreateExternalSource(int userId, ExternalSourceDto dto,
        CancellationToken ct = default)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId,
            AppUserIncludes.ExternalSources, ct);
        if (user == null) throw new KavitaException("not-authenticated");

        if (user.ExternalSources.Any(s => s.Host == dto.Host))
        {
            throw new KavitaException("external-source-already-exists");
        }

        if (string.IsNullOrEmpty(dto.Name)) throw new KavitaException("external-source-required");
        if (!UrlHelper.StartsWithHttpOrHttps(dto.Host)) throw new KavitaException("external-source-host-format");


        var newSource = new AppUserExternalSource()
        {
            Name = dto.Name,
            Host = UrlHelper.EnsureEndsWithSlash(UrlHelper.EnsureStartsWithHttpOrHttps(dto.Host)),
            ApiKey = dto.ApiKey
        };
        user.ExternalSources.Add(newSource);

        unitOfWork.UserRepository.Update(user);
        await unitOfWork.CommitAsync(ct);

        dto.Id = newSource.Id;

        return dto;
    }

    public async Task<ExternalSourceDto> UpdateExternalSource(int userId, ExternalSourceDto dto,
        CancellationToken ct = default)
    {
        var source = await unitOfWork.AppUserExternalSourceRepository.GetById(dto.Id, ct);
        if (source == null) throw new KavitaException("external-source-doesnt-exist");
        if (source.AppUserId != userId) throw new KavitaException("external-source-doesnt-exist");

        if (string.IsNullOrEmpty(dto.Host) || string.IsNullOrEmpty(dto.Name)) throw new KavitaException("external-source-required");

        source.Host = UrlHelper.EnsureEndsWithSlash(
            UrlHelper.EnsureStartsWithHttpOrHttps(dto.Host));
        source.ApiKey = dto.ApiKey;
        source.Name = dto.Name;

        unitOfWork.AppUserExternalSourceRepository.Update(source);
        await unitOfWork.CommitAsync(ct);

        dto.Host = source.Host;
        return dto;
    }

    public async Task DeleteExternalSource(int userId, int externalSourceId, CancellationToken ct = default)
    {
        var source = await unitOfWork.AppUserExternalSourceRepository.GetById(externalSourceId, ct);
        if (source == null) throw new KavitaException("external-source-doesnt-exist");
        if (source.AppUserId != userId) throw new KavitaException("external-source-doesnt-exist");

        unitOfWork.AppUserExternalSourceRepository.Delete(source);

        // Find all SideNav's with this source and delete them as well
        var streams2 = await unitOfWork.UserRepository.GetSideNavStreamWithExternalSource(externalSourceId, ct);
        unitOfWork.UserRepository.Delete(streams2);

        await unitOfWork.CommitAsync(ct);
    }

    public async Task DeleteSideNavSmartFilterStream(int userId, int sideNavStreamId, CancellationToken ct = default)
    {
        try
        {
            var stream = await unitOfWork.UserRepository.GetSideNavStream(sideNavStreamId, ct);
            if (stream == null) throw new KavitaException("sidenav-stream-doesnt-exist");

            if (stream.AppUserId != userId) throw new KavitaException("sidenav-stream-doesnt-exist");


            if (stream.StreamType != SideNavStreamType.SmartFilter)
            {
                throw new KavitaException("sidenav-stream-only-delete-smart-filter");
            }

            unitOfWork.UserRepository.Delete(stream);

            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception deleting SideNav Smart Filter Stream: {FilterId}", sideNavStreamId);
            throw;
        }
    }

    public async Task DeleteDashboardSmartFilterStream(int userId, int dashboardStreamId, CancellationToken ct = default)
    {
        try
        {
            var stream = await unitOfWork.UserRepository.GetDashboardStream(dashboardStreamId, ct);
            if (stream == null) throw new KavitaException("dashboard-stream-doesnt-exist");

            if (stream.AppUserId != userId) throw new KavitaException("dashboard-stream-doesnt-exist");

            if (stream.StreamType != DashboardStreamType.SmartFilter)
            {
                throw new KavitaException("dashboard-stream-only-delete-smart-filter");
            }

            unitOfWork.UserRepository.Delete(stream);

            await unitOfWork.CommitAsync(ct);
        } catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception deleting Dashboard Smart Filter Stream: {FilterId}", dashboardStreamId);
            throw;
        }
    }

    public async Task RenameSmartFilterStreams(AppUserSmartFilter smartFilter, CancellationToken ct = default)
    {
        var sideNavStreams = await unitOfWork.UserRepository.GetSideNavStreamWithFilter(smartFilter.Id, ct);
        var dashboardStreams = await unitOfWork.UserRepository.GetDashboardStreamWithFilter(smartFilter.Id, ct);

        foreach (var sideNavStream in sideNavStreams)
        {
            sideNavStream.Name = smartFilter.Name;
        }

        foreach (var dashboardStream in dashboardStreams)
        {
            dashboardStream.Name = smartFilter.Name;
        }

        await unitOfWork.CommitAsync(ct);
    }
}
