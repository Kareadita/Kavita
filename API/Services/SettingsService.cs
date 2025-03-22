using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.KavitaPlus.Metadata;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Logging;
using API.Services.Tasks.Scanner;
using Hangfire;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Services;

public interface ISettingsService
{
    Task<ActionResult<MetadataSettingsDto>> UpdateMetadataSettings(MetadataSettingsDto dto);
    Task<ActionResult<ServerSettingDto>> UpdateSettings(ServerSettingDto updateSettingsDto);
}


public class SettingsService : ISettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;
    private readonly ILibraryWatcher _libraryWatcher;
    private readonly ITaskScheduler _taskScheduler;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(IUnitOfWork unitOfWork, IDirectoryService directoryService,
        ILibraryWatcher libraryWatcher, ITaskScheduler taskScheduler,
        ILogger<SettingsService> logger)
    {
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
        _libraryWatcher = libraryWatcher;
        _taskScheduler = taskScheduler;
        _logger = logger;
    }

    /// <summary>
    /// Update the metadata settings for Kavita+ Metadata feature
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    public async Task<ActionResult<MetadataSettingsDto>> UpdateMetadataSettings(MetadataSettingsDto dto)
    {
        var existingMetadataSetting = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        existingMetadataSetting.Enabled = dto.Enabled;
        existingMetadataSetting.EnableSummary = dto.EnableSummary;
        existingMetadataSetting.EnableLocalizedName = dto.EnableLocalizedName;
        existingMetadataSetting.EnablePublicationStatus = dto.EnablePublicationStatus;
        existingMetadataSetting.EnableRelationships = dto.EnableRelationships;
        existingMetadataSetting.EnablePeople = dto.EnablePeople;
        existingMetadataSetting.EnableStartDate = dto.EnableStartDate;
        existingMetadataSetting.EnableGenres = dto.EnableGenres;
        existingMetadataSetting.EnableTags = dto.EnableTags;
        existingMetadataSetting.FirstLastPeopleNaming = dto.FirstLastPeopleNaming;
        existingMetadataSetting.EnableCoverImage = dto.EnableCoverImage;

        existingMetadataSetting.AgeRatingMappings = dto.AgeRatingMappings ?? [];

        existingMetadataSetting.Blacklist = (dto.Blacklist ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).DistinctBy(d => d.ToNormalized()).ToList() ?? [];
        existingMetadataSetting.Whitelist = (dto.Whitelist ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).DistinctBy(d => d.ToNormalized()).ToList() ?? [];
        existingMetadataSetting.Overrides = [.. dto.Overrides ?? []];
        existingMetadataSetting.PersonRoles = dto.PersonRoles ?? [];

        // Handle Field Mappings

        // Clear existing mappings
        existingMetadataSetting.FieldMappings ??= [];
        _unitOfWork.SettingsRepository.RemoveRange(existingMetadataSetting.FieldMappings);
        existingMetadataSetting.FieldMappings.Clear();

        if (dto.FieldMappings != null)
        {
            // Add new mappings
            foreach (var mappingDto in dto.FieldMappings)
            {
                existingMetadataSetting.FieldMappings.Add(new MetadataFieldMapping
                {
                    SourceType = mappingDto.SourceType,
                    DestinationType = mappingDto.DestinationType,
                    SourceValue = mappingDto.SourceValue,
                    DestinationValue = mappingDto.DestinationValue,
                    ExcludeFromSource = mappingDto.ExcludeFromSource
                });
            }
        }

        // Save changes
        await _unitOfWork.CommitAsync();

        // Return updated settings
        return await _unitOfWork.SettingsRepository.GetMetadataSettingDto();
    }

    /// <summary>
    /// Update Server Settings
    /// </summary>
    /// <param name="updateSettingsDto"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    public async Task<ActionResult<ServerSettingDto>> UpdateSettings(ServerSettingDto updateSettingsDto)
    {
        // We do not allow CacheDirectory changes, so we will ignore.
        var currentSettings = await _unitOfWork.SettingsRepository.GetSettingsAsync();
        var updateBookmarks = false;
        var originalBookmarkDirectory = _directoryService.BookmarkDirectory;

        var bookmarkDirectory = updateSettingsDto.BookmarksDirectory;
        if (!updateSettingsDto.BookmarksDirectory.EndsWith("bookmarks") &&
            !updateSettingsDto.BookmarksDirectory.EndsWith("bookmarks/"))
        {
            bookmarkDirectory =
                _directoryService.FileSystem.Path.Join(updateSettingsDto.BookmarksDirectory, "bookmarks");
        }

        if (string.IsNullOrEmpty(updateSettingsDto.BookmarksDirectory))
        {
            bookmarkDirectory = _directoryService.BookmarkDirectory;
        }

        var updateTask = false;
        foreach (var setting in currentSettings)
        {
            if (setting.Key == ServerSettingKey.OnDeckProgressDays &&
                updateSettingsDto.OnDeckProgressDays + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.OnDeckProgressDays + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.OnDeckUpdateDays &&
                updateSettingsDto.OnDeckUpdateDays + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.OnDeckUpdateDays + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.Port && updateSettingsDto.Port + string.Empty != setting.Value)
            {
                if (OsInfo.IsDocker) continue;
                setting.Value = updateSettingsDto.Port + string.Empty;
                // Port is managed in appSetting.json
                Configuration.Port = updateSettingsDto.Port;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.CacheSize &&
                updateSettingsDto.CacheSize + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.CacheSize + string.Empty;
                // CacheSize is managed in appSetting.json
                Configuration.CacheSize = updateSettingsDto.CacheSize;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            updateTask = updateTask || UpdateSchedulingSettings(setting, updateSettingsDto);

            UpdateEmailSettings(setting, updateSettingsDto);



            if (setting.Key == ServerSettingKey.IpAddresses && updateSettingsDto.IpAddresses != setting.Value)
            {
                if (OsInfo.IsDocker) continue;
                // Validate IP addresses
                foreach (var ipAddress in updateSettingsDto.IpAddresses.Split(',',
                             StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!IPAddress.TryParse(ipAddress.Trim(), out _))
                    {
                        throw new KavitaException("ip-address-invalid");
                    }
                }

                setting.Value = updateSettingsDto.IpAddresses;
                // IpAddresses is managed in appSetting.json
                Configuration.IpAddresses = updateSettingsDto.IpAddresses;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.BaseUrl && updateSettingsDto.BaseUrl + string.Empty != setting.Value)
            {
                var path = !updateSettingsDto.BaseUrl.StartsWith('/')
                    ? $"/{updateSettingsDto.BaseUrl}"
                    : updateSettingsDto.BaseUrl;
                path = !path.EndsWith('/')
                    ? $"{path}/"
                    : path;
                setting.Value = path;
                Configuration.BaseUrl = updateSettingsDto.BaseUrl;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.LoggingLevel &&
                updateSettingsDto.LoggingLevel + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.LoggingLevel + string.Empty;
                LogLevelOptions.SwitchLogLevel(updateSettingsDto.LoggingLevel);
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.EnableOpds &&
                updateSettingsDto.EnableOpds + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.EnableOpds + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.EncodeMediaAs &&
                ((int)updateSettingsDto.EncodeMediaAs).ToString() != setting.Value)
            {
                setting.Value = ((int)updateSettingsDto.EncodeMediaAs).ToString();
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.CoverImageSize &&
                ((int)updateSettingsDto.CoverImageSize).ToString() != setting.Value)
            {
                setting.Value = ((int)updateSettingsDto.CoverImageSize).ToString();
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.HostName && updateSettingsDto.HostName + string.Empty != setting.Value)
            {
                setting.Value = (updateSettingsDto.HostName + string.Empty).Trim();
                setting.Value = UrlHelper.RemoveEndingSlash(setting.Value);
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.BookmarkDirectory && bookmarkDirectory != setting.Value)
            {
                // Validate new directory can be used
                if (!await _directoryService.CheckWriteAccess(bookmarkDirectory))
                {
                    throw new KavitaException("bookmark-dir-permissions");
                }

                originalBookmarkDirectory = setting.Value;

                // Normalize the path deliminators. Just to look nice in DB, no functionality
                setting.Value = _directoryService.FileSystem.Path.GetFullPath(bookmarkDirectory);
                _unitOfWork.SettingsRepository.Update(setting);
                updateBookmarks = true;

            }

            if (setting.Key == ServerSettingKey.AllowStatCollection &&
                updateSettingsDto.AllowStatCollection + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.AllowStatCollection + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.TotalBackups &&
                updateSettingsDto.TotalBackups + string.Empty != setting.Value)
            {
                if (updateSettingsDto.TotalBackups > 30 || updateSettingsDto.TotalBackups < 1)
                {
                    throw new KavitaException("total-backups");
                }

                setting.Value = updateSettingsDto.TotalBackups + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.TotalLogs &&
                updateSettingsDto.TotalLogs + string.Empty != setting.Value)
            {
                if (updateSettingsDto.TotalLogs > 30 || updateSettingsDto.TotalLogs < 1)
                {
                    throw new KavitaException("total-logs");
                }

                setting.Value = updateSettingsDto.TotalLogs + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.EnableFolderWatching &&
                updateSettingsDto.EnableFolderWatching + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.EnableFolderWatching + string.Empty;
                _unitOfWork.SettingsRepository.Update(setting);
            }
        }

        if (!_unitOfWork.HasChanges()) return updateSettingsDto;

        try
        {
            await _unitOfWork.CommitAsync();

            if (!updateSettingsDto.AllowStatCollection)
            {
                _taskScheduler.CancelStatsTasks();
            }
            else
            {
                await _taskScheduler.ScheduleStatsTasks();
            }

            if (updateBookmarks)
            {
                UpdateBookmarkDirectory(originalBookmarkDirectory, bookmarkDirectory);
            }

            if (updateTask)
            {
                BackgroundJob.Enqueue(() => _taskScheduler.ScheduleTasks());
            }

            if (updateSettingsDto.EnableFolderWatching)
            {
                BackgroundJob.Enqueue(() => _libraryWatcher.StartWatching());
            }
            else
            {
                BackgroundJob.Enqueue(() => _libraryWatcher.StopWatching());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when updating server settings");
            await _unitOfWork.RollbackAsync();
            throw new KavitaException("generic-error");
        }


        _logger.LogInformation("Server Settings updated");

        return updateSettingsDto;
    }

    private void UpdateBookmarkDirectory(string originalBookmarkDirectory, string bookmarkDirectory)
    {
        _directoryService.ExistOrCreate(bookmarkDirectory);
        _directoryService.CopyDirectoryToDirectory(originalBookmarkDirectory, bookmarkDirectory);
        _directoryService.ClearAndDeleteDirectory(originalBookmarkDirectory);
    }

    private bool UpdateSchedulingSettings(ServerSetting setting, ServerSettingDto updateSettingsDto)
    {
        if (setting.Key == ServerSettingKey.TaskBackup && updateSettingsDto.TaskBackup != setting.Value)
        {
            setting.Value = updateSettingsDto.TaskBackup;
            _unitOfWork.SettingsRepository.Update(setting);

            return true;
        }

        if (setting.Key == ServerSettingKey.TaskScan && updateSettingsDto.TaskScan != setting.Value)
        {
            setting.Value = updateSettingsDto.TaskScan;
            _unitOfWork.SettingsRepository.Update(setting);
            return true;
        }

        if (setting.Key == ServerSettingKey.TaskCleanup && updateSettingsDto.TaskCleanup != setting.Value)
        {
            setting.Value = updateSettingsDto.TaskCleanup;
            _unitOfWork.SettingsRepository.Update(setting);
            return true;
        }
        return false;
    }

    private void UpdateEmailSettings(ServerSetting setting, ServerSettingDto updateSettingsDto)
    {
        if (setting.Key == ServerSettingKey.EmailHost &&
            updateSettingsDto.SmtpConfig.Host + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.Host + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailPort &&
            updateSettingsDto.SmtpConfig.Port + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.Port + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailAuthPassword &&
            updateSettingsDto.SmtpConfig.Password + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.Password + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailAuthUserName &&
            updateSettingsDto.SmtpConfig.UserName + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.UserName + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailSenderAddress &&
            updateSettingsDto.SmtpConfig.SenderAddress + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.SenderAddress + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailSenderDisplayName &&
            updateSettingsDto.SmtpConfig.SenderDisplayName + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.SenderDisplayName + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailSizeLimit &&
            updateSettingsDto.SmtpConfig.SizeLimit + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.SizeLimit + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailEnableSsl &&
            updateSettingsDto.SmtpConfig.EnableSsl + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.EnableSsl + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailCustomizedTemplates &&
            updateSettingsDto.SmtpConfig.CustomizedTemplates + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.CustomizedTemplates + string.Empty;
            _unitOfWork.SettingsRepository.Update(setting);
        }
    }
}
