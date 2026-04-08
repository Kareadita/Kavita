using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Kavita.Services;


public class SettingsService(
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    ILibraryWatcher libraryWatcher,
    ITaskScheduler taskScheduler,
    ILogger<SettingsService> logger,
    IOidcService oidcService,
    ILoggingService loggingService)
    : ISettingsService
{
    private readonly bool _isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Development;

    /// <summary>
    /// Update the metadata settings for Kavita+ Metadata feature
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<MetadataSettingsDto> UpdateMetadataSettings(MetadataSettingsDto dto, CancellationToken ct = default)
    {
        var existingMetadataSetting = await unitOfWork.SettingsRepository.GetMetadataSettings(ct);
        existingMetadataSetting.Enabled = dto.Enabled;
        existingMetadataSetting.EnableExtendedMetadataProcessing = dto.EnableExtendedMetadataProcessing;
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

        existingMetadataSetting.EnableChapterPublisher = dto.EnableChapterPublisher;
        existingMetadataSetting.EnableChapterSummary = dto.EnableChapterSummary;
        existingMetadataSetting.EnableChapterTitle = dto.EnableChapterTitle;
        existingMetadataSetting.EnableChapterReleaseDate = dto.EnableChapterReleaseDate;
        existingMetadataSetting.EnableChapterCoverImage = dto.EnableChapterCoverImage;

        existingMetadataSetting.AgeRatingMappings = dto.AgeRatingMappings ?? [];

        existingMetadataSetting.Blacklist = (dto.Blacklist ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).DistinctBy(d => d.ToNormalized()).ToList() ?? [];
        existingMetadataSetting.Whitelist = (dto.Whitelist ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).DistinctBy(d => d.ToNormalized()).ToList() ?? [];
        existingMetadataSetting.Overrides = [.. dto.Overrides ?? []];
        existingMetadataSetting.PersonRoles = dto.PersonRoles ?? [];

        // Handle Field Mappings

        // Clear existing mappings
        existingMetadataSetting.FieldMappings ??= [];
        unitOfWork.SettingsRepository.RemoveRange(existingMetadataSetting.FieldMappings);
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
        await unitOfWork.CommitAsync(ct);

        // Return updated settings
        return await unitOfWork.SettingsRepository.GetMetadataSettingDto(ct);
    }

    public async Task<FieldMappingsImportResultDto> ImportFieldMappings(FieldMappingsDto dto,
        ImportSettingsDto settings, CancellationToken ct = default)
    {
        if (dto.AgeRatingMappings.Keys.Distinct().Count() != dto.AgeRatingMappings.Count)
        {
            throw new KavitaException("errors.import-fields.non-unique-age-ratings");
        }

        if (dto.FieldMappings.DistinctBy(f => f.Id).Count() != dto.FieldMappings.Count)
        {
            throw new KavitaException("errors.import-fields.non-unique-fields");
        }

        return settings.ImportMode switch
        {
            ImportMode.Merge => await MergeFieldMappings(dto, settings),
            ImportMode.Replace => await ReplaceFieldMappings(dto, settings),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), $"Invalid import mode {nameof(settings.ImportMode)}")
        };
    }

    /// <summary>
    /// Will fully replace any enabled fields, always successful
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="settings"></param>
    /// <returns></returns>
    private async Task<FieldMappingsImportResultDto> ReplaceFieldMappings(FieldMappingsDto dto, ImportSettingsDto settings)
    {
        var existingMetadataSetting = await unitOfWork.SettingsRepository.GetMetadataSettingDto();

        if (settings.Whitelist)
        {
            existingMetadataSetting.Whitelist = dto.Whitelist;
        }

        if (settings.Blacklist)
        {
            existingMetadataSetting.Blacklist = dto.Blacklist;
        }

        if (settings.AgeRatings)
        {
            existingMetadataSetting.AgeRatingMappings = dto.AgeRatingMappings;
        }

        if (settings.FieldMappings)
        {
            existingMetadataSetting.FieldMappings = dto.FieldMappings;
        }

        return new FieldMappingsImportResultDto
        {
            Success = true,
            ResultingMetadataSettings = existingMetadataSetting,
            AgeRatingConflicts = [],
        };
    }

    /// <summary>
    /// Tries to merge all enabled fields, fails if any merge was marked as manual. Always goes through all items
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="settings"></param>
    /// <returns></returns>
    private async Task<FieldMappingsImportResultDto> MergeFieldMappings(FieldMappingsDto dto, ImportSettingsDto settings)
    {
        var existingMetadataSetting = await unitOfWork.SettingsRepository.GetMetadataSettingDto();

        if (settings.Whitelist)
        {
            existingMetadataSetting.Whitelist = existingMetadataSetting.Whitelist.Union(dto.Whitelist).DistinctBy(d => d.ToNormalized()).ToList();
        }

        if (settings.Blacklist)
        {
            existingMetadataSetting.Blacklist = existingMetadataSetting.Blacklist.Union(dto.Blacklist).DistinctBy(d => d.ToNormalized()).ToList();
        }

        List<string> ageRatingConflicts = [];

        if (settings.AgeRatings)
        {
            foreach (var arm in dto.AgeRatingMappings)
            {
                if (!existingMetadataSetting.AgeRatingMappings.TryGetValue(arm.Key, out var mapping))
                {
                    existingMetadataSetting.AgeRatingMappings.Add(arm.Key, arm.Value);
                    continue;
                }

                if (arm.Value == mapping)
                {
                    continue;
                }

                var resolution = settings.AgeRatingConflictResolutions.GetValueOrDefault(arm.Key, settings.Resolution);

                switch (resolution)
                {
                    case ConflictResolution.Keep: continue;
                    case ConflictResolution.Replace:
                        existingMetadataSetting.AgeRatingMappings[arm.Key] = arm.Value;
                        break;
                    case ConflictResolution.Manual:
                        ageRatingConflicts.Add(arm.Key);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(settings), $"Invalid conflict resolution {nameof(ConflictResolution)}.");
                }
            }
        }


        if (settings.FieldMappings)
        {
            existingMetadataSetting.FieldMappings = existingMetadataSetting.FieldMappings
                .Union(dto.FieldMappings)
                .DistinctBy(fm => new
                {
                    fm.SourceType,
                    SourceValue = fm.SourceValue.ToNormalized(),
                    fm.DestinationType,
                    DestinationValue = fm.DestinationValue.ToNormalized(),
                })
                .ToList();
        }

        if (ageRatingConflicts.Count > 0)
        {
            return new FieldMappingsImportResultDto
            {
                Success = false,
                AgeRatingConflicts = ageRatingConflicts,
            };
        }

        return new FieldMappingsImportResultDto
        {
            Success = true,
            ResultingMetadataSettings = existingMetadataSetting,
            AgeRatingConflicts = [],
        };
    }

    /// <summary>
    /// Update Server Settings
    /// </summary>
    /// <param name="updateSettingsDto"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    public async Task<ServerSettingDto> UpdateSettings(ServerSettingDto updateSettingsDto, CancellationToken ct = default)
    {
        // We do not allow CacheDirectory changes, so we will ignore.
        var currentSettings = await unitOfWork.SettingsRepository.GetSettingsAsync(ct);
        var updateBookmarks = false;
        var originalBookmarkDirectory = directoryService.BookmarkDirectory;

        var bookmarkDirectory = updateSettingsDto.BookmarksDirectory;
        if (!updateSettingsDto.BookmarksDirectory.EndsWith("bookmarks") &&
            !updateSettingsDto.BookmarksDirectory.EndsWith("bookmarks/"))
        {
            bookmarkDirectory =
                directoryService.FileSystem.Path.Join(updateSettingsDto.BookmarksDirectory, "bookmarks");
        }

        if (string.IsNullOrEmpty(updateSettingsDto.BookmarksDirectory))
        {
            bookmarkDirectory = directoryService.BookmarkDirectory;
        }

        var updateTask = false;
        var updatedOidcSettings = false;
        foreach (var setting in currentSettings)
        {
            if (setting.Key == ServerSettingKey.OnDeckProgressDays &&
                updateSettingsDto.OnDeckProgressDays + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.OnDeckProgressDays + string.Empty;
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.OnDeckUpdateDays &&
                updateSettingsDto.OnDeckUpdateDays + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.OnDeckUpdateDays + string.Empty;
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.Port && updateSettingsDto.Port + string.Empty != setting.Value)
            {
                if (OsInfo.IsDocker) continue;
                setting.Value = updateSettingsDto.Port + string.Empty;
                // Port is managed in appSetting.json
                Configuration.Port = updateSettingsDto.Port;
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.CacheSize &&
                updateSettingsDto.CacheSize + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.CacheSize + string.Empty;
                // CacheSize is managed in appSetting.json
                Configuration.CacheSize = updateSettingsDto.CacheSize;
                unitOfWork.SettingsRepository.Update(setting);
            }

            updateTask = updateTask || UpdateSchedulingSettings(setting, updateSettingsDto);

            UpdateEmailSettings(setting, updateSettingsDto);
            updatedOidcSettings = await UpdateOidcSettings(setting, updateSettingsDto) || updatedOidcSettings;


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
                unitOfWork.SettingsRepository.Update(setting);
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
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.LoggingLevel &&
                updateSettingsDto.LoggingLevel + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.LoggingLevel + string.Empty;
                loggingService.SwitchLogLevel(updateSettingsDto.LoggingLevel);
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.EnableOpds &&
                updateSettingsDto.EnableOpds + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.EnableOpds + string.Empty;
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.EncodeMediaAs &&
                ((int)updateSettingsDto.EncodeMediaAs).ToString() != setting.Value)
            {
                setting.Value = ((int)updateSettingsDto.EncodeMediaAs).ToString();
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.CoverImageSize &&
                ((int)updateSettingsDto.CoverImageSize).ToString() != setting.Value)
            {
                setting.Value = ((int)updateSettingsDto.CoverImageSize).ToString();
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.PdfRenderResolution &&
                ((int)updateSettingsDto.PdfRenderResolution).ToString() != setting.Value)
            {
                setting.Value = ((int)updateSettingsDto.PdfRenderResolution).ToString();
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.HostName && updateSettingsDto.HostName + string.Empty != setting.Value)
            {
                setting.Value = (updateSettingsDto.HostName + string.Empty).Trim();
                setting.Value = UrlHelper.RemoveEndingSlash(setting.Value);
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.BookmarkDirectory && bookmarkDirectory != setting.Value)
            {
                // Validate new directory can be used
                if (!await directoryService.CheckWriteAccess(bookmarkDirectory))
                {
                    throw new KavitaException("bookmark-dir-permissions");
                }

                originalBookmarkDirectory = setting.Value;

                // Normalize the path deliminators. Just to look nice in DB, no functionality
                setting.Value = directoryService.FileSystem.Path.GetFullPath(bookmarkDirectory);
                unitOfWork.SettingsRepository.Update(setting);
                updateBookmarks = true;

            }

            if (setting.Key == ServerSettingKey.AllowStatCollection &&
                updateSettingsDto.AllowStatCollection + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.AllowStatCollection + string.Empty;
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.TotalBackups &&
                updateSettingsDto.TotalBackups + string.Empty != setting.Value)
            {
                if (updateSettingsDto.TotalBackups > 30 || updateSettingsDto.TotalBackups < 1)
                {
                    throw new KavitaException("total-backups");
                }

                setting.Value = updateSettingsDto.TotalBackups + string.Empty;
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.TotalLogs &&
                updateSettingsDto.TotalLogs + string.Empty != setting.Value)
            {
                if (updateSettingsDto.TotalLogs > 30 || updateSettingsDto.TotalLogs < 1)
                {
                    throw new KavitaException("total-logs");
                }

                setting.Value = updateSettingsDto.TotalLogs + string.Empty;
                unitOfWork.SettingsRepository.Update(setting);
            }

            if (setting.Key == ServerSettingKey.EnableFolderWatching &&
                updateSettingsDto.EnableFolderWatching + string.Empty != setting.Value)
            {
                setting.Value = updateSettingsDto.EnableFolderWatching + string.Empty;
                unitOfWork.SettingsRepository.Update(setting);
            }
        }

        if (!unitOfWork.HasChanges()) return updateSettingsDto;

        try
        {
            await unitOfWork.CommitAsync(ct);

            if (!updateSettingsDto.AllowStatCollection)
            {
                taskScheduler.CancelStatsTasks();
            }
            else
            {
                await taskScheduler.ScheduleStatsTasks();
            }

            if (updateBookmarks)
            {
                UpdateBookmarkDirectory(originalBookmarkDirectory, bookmarkDirectory);
            }

            if (updateTask)
            {
                BackgroundJob.Enqueue(() => taskScheduler.ScheduleTasks());
            }

            if (updatedOidcSettings)
            {
                Configuration.OidcSettings = new Configuration.OpenIdConnectSettings
                {
                    Authority = updateSettingsDto.OidcConfig.Authority,
                    ClientId = updateSettingsDto.OidcConfig.ClientId,
                    Secret = updateSettingsDto.OidcConfig.Secret,
                    CustomScopes = updateSettingsDto.OidcConfig.CustomScopes,
                };
            }

            if (updateSettingsDto.EnableFolderWatching)
            {
                BackgroundJob.Enqueue(() => libraryWatcher.StartWatching());
            }
            else
            {
                BackgroundJob.Enqueue(() => libraryWatcher.StopWatching());
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when updating server settings");
            await unitOfWork.RollbackAsync(ct);
            throw new KavitaException("generic-error");
        }


        logger.LogInformation("Server Settings updated");

        return updateSettingsDto;
    }

    public async Task<AuthorityValidationResult> IsValidAuthority(string authority, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(authority))
        {
            return AuthorityValidationResult.NotApplicable;
        }

        if (!_isDevelopment && !authority.StartsWith("https"))
        {
            return AuthorityValidationResult.MissingHttps;
        }

        try
        {
            var hasTrailingSlash = authority.EndsWith('/');
            var url = authority + (hasTrailingSlash ? string.Empty : "/") + ".well-known/openid-configuration";

            var json = await FlurlConfiguration.CreateSafeRequest(url)
                .GetStringAsync(cancellationToken: ct);
            var config = OpenIdConnectConfiguration.Create(json);
            return config.Issuer == authority ? AuthorityValidationResult.Success : AuthorityValidationResult.InvalidAuthority;
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "OpenIdConfiguration failed: {Reason}", e.Message);
            return AuthorityValidationResult.Failure;
        }
    }

    private void UpdateBookmarkDirectory(string originalBookmarkDirectory, string bookmarkDirectory)
    {
        directoryService.ExistOrCreate(bookmarkDirectory);
        directoryService.CopyDirectoryToDirectory(originalBookmarkDirectory, bookmarkDirectory);
        directoryService.ClearAndDeleteDirectory(originalBookmarkDirectory);
    }

    private bool UpdateSchedulingSettings(ServerSetting setting, ServerSettingDto updateSettingsDto)
    {
        if (setting.Key == ServerSettingKey.TaskBackup && updateSettingsDto.TaskBackup != setting.Value)
        {
            setting.Value = updateSettingsDto.TaskBackup;
            unitOfWork.SettingsRepository.Update(setting);

            return true;
        }

        if (setting.Key == ServerSettingKey.TaskScan && updateSettingsDto.TaskScan != setting.Value)
        {
            setting.Value = updateSettingsDto.TaskScan;
            unitOfWork.SettingsRepository.Update(setting);
            return true;
        }

        if (setting.Key == ServerSettingKey.TaskCleanup && updateSettingsDto.TaskCleanup != setting.Value)
        {
            setting.Value = updateSettingsDto.TaskCleanup;
            unitOfWork.SettingsRepository.Update(setting);
            return true;
        }

        if (setting.Key == ServerSettingKey.TaskCblSync && updateSettingsDto.TaskCblSync != setting.Value)
        {
            setting.Value = updateSettingsDto.TaskCblSync;
            unitOfWork.SettingsRepository.Update(setting);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Updates oidc settings and return true if a change was made
    /// </summary>
    /// <param name="setting"></param>
    /// <param name="updateSettingsDto"></param>
    /// <returns></returns>
    /// <remarks>Does not commit any changes</remarks>
    /// <exception cref="KavitaException">If the authority is invalid</exception>
    private async Task<bool> UpdateOidcSettings(ServerSetting setting, ServerSettingDto updateSettingsDto)
    {
        if (setting.Key != ServerSettingKey.OidcConfiguration) return false;

        if (updateSettingsDto.OidcConfig.RolesClaim.Trim() == string.Empty)
        {
            updateSettingsDto.OidcConfig.RolesClaim = ClaimTypes.Role;
        }

        var currentConfig = JsonSerializer.Deserialize<OidcConfigDto>(setting.Value)!;

        // Patch Oidc Secret back in if not changed
        if ("*".Repeat(currentConfig.Secret.Length) == updateSettingsDto.OidcConfig.Secret)
        {
            updateSettingsDto.OidcConfig.Secret = currentConfig.Secret;
        }

        var newValue = JsonSerializer.Serialize(updateSettingsDto.OidcConfig);
        if (setting.Value == newValue) return false;

        if (currentConfig.Authority != updateSettingsDto.OidcConfig.Authority)
        {
            // Only check validity if we're changing into a value that would be used
            if (!string.IsNullOrEmpty(updateSettingsDto.OidcConfig.Authority)
                && await IsValidAuthority(updateSettingsDto.OidcConfig.Authority + string.Empty) != AuthorityValidationResult.Success)
            {
                throw new KavitaException("oidc-invalid-authority");
            }

            logger.LogWarning("OIDC Authority is changing, clearing all external ids");
            await oidcService.ClearOidcIds();
        }

        setting.Value = newValue;
        unitOfWork.SettingsRepository.Update(setting);

        return true;
    }

    private void UpdateEmailSettings(ServerSetting setting, ServerSettingDto updateSettingsDto)
    {
        if (setting.Key == ServerSettingKey.EmailHost &&
            updateSettingsDto.SmtpConfig.Host + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.Host + string.Empty;
            unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailPort &&
            updateSettingsDto.SmtpConfig.Port + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.Port + string.Empty;
            unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailAuthPassword &&
            updateSettingsDto.SmtpConfig.Password + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.Password + string.Empty;
            unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailAuthUserName &&
            updateSettingsDto.SmtpConfig.UserName + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.UserName + string.Empty;
            unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailSenderAddress &&
            updateSettingsDto.SmtpConfig.SenderAddress + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.SenderAddress + string.Empty;
            unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailSenderDisplayName &&
            updateSettingsDto.SmtpConfig.SenderDisplayName + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.SenderDisplayName + string.Empty;
            unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailSizeLimit &&
            updateSettingsDto.SmtpConfig.SizeLimit + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.SizeLimit + string.Empty;
            unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailEnableSsl &&
            updateSettingsDto.SmtpConfig.EnableSsl + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.EnableSsl + string.Empty;
            unitOfWork.SettingsRepository.Update(setting);
        }

        if (setting.Key == ServerSettingKey.EmailCustomizedTemplates &&
            updateSettingsDto.SmtpConfig.CustomizedTemplates + string.Empty != setting.Value)
        {
            setting.Value = updateSettingsDto.SmtpConfig.CustomizedTemplates + string.Empty;
            unitOfWork.SettingsRepository.Update(setting);
        }
    }
}
