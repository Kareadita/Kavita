using System;
using System.Collections.Generic;
using System.Globalization;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using AutoMapper;

namespace API.Helpers.Converters;
#nullable enable

public class ServerSettingConverter : ITypeConverter<IEnumerable<ServerSetting>, ServerSettingDto>
{
    public ServerSettingDto Convert(IEnumerable<ServerSetting> source, ServerSettingDto destination, ResolutionContext context)
    {
        destination ??= new ServerSettingDto();
        foreach (var row in source)
        {
            switch (row.Key)
            {
                case ServerSettingKey.CacheDirectory:
                    destination.CacheDirectory = row.Value;
                    break;
                case ServerSettingKey.TaskScan:
                    destination.TaskScan = row.Value;
                    break;
                case ServerSettingKey.TaskBackup:
                    destination.TaskBackup = row.Value;
                    break;
                case ServerSettingKey.TaskCleanup:
                    destination.TaskCleanup = row.Value;
                    break;
                case ServerSettingKey.LoggingLevel:
                    destination.LoggingLevel = row.Value;
                    break;
                case ServerSettingKey.Port:
                    destination.Port = int.Parse(row.Value, CultureInfo.InvariantCulture);
                    break;
                case ServerSettingKey.IpAddresses:
                    destination.IpAddresses = row.Value;
                    break;
                case ServerSettingKey.AllowStatCollection:
                    destination.AllowStatCollection = bool.Parse(row.Value);
                    break;
                case ServerSettingKey.EnableOpds:
                    destination.EnableOpds = bool.Parse(row.Value);
                    break;
                case ServerSettingKey.BaseUrl:
                    destination.BaseUrl = row.Value;
                    break;
                case ServerSettingKey.BookmarkDirectory:
                    destination.BookmarksDirectory = row.Value;
                    break;
                case ServerSettingKey.InstallVersion:
                    destination.InstallVersion = row.Value;
                    break;
                case ServerSettingKey.TotalBackups:
                    destination.TotalBackups = int.Parse(row.Value, CultureInfo.InvariantCulture);
                    break;
                case ServerSettingKey.InstallId:
                    destination.InstallId = row.Value;
                    break;
                case ServerSettingKey.EnableFolderWatching:
                    destination.EnableFolderWatching = bool.Parse(row.Value);
                    break;
                case ServerSettingKey.TotalLogs:
                    destination.TotalLogs = int.Parse(row.Value, CultureInfo.InvariantCulture);
                    break;
                case ServerSettingKey.HostName:
                    destination.HostName = row.Value;
                    break;
                case ServerSettingKey.CacheSize:
                    destination.CacheSize = long.Parse(row.Value, CultureInfo.InvariantCulture);
                    break;
                case ServerSettingKey.OnDeckProgressDays:
                    destination.OnDeckProgressDays = int.Parse(row.Value, CultureInfo.InvariantCulture);
                    break;
                case ServerSettingKey.OnDeckUpdateDays:
                    destination.OnDeckUpdateDays = int.Parse(row.Value, CultureInfo.InvariantCulture);
                    break;
                case ServerSettingKey.CoverImageSize:
                    destination.CoverImageSize = Enum.Parse<CoverImageSize>(row.Value);
                    break;
                case ServerSettingKey.EncodeMediaAs:
                    destination.EncodeMediaAs = Enum.Parse<EncodeFormat>(row.Value);
                    break;
                case ServerSettingKey.BackupDirectory:
                    destination.BookmarksDirectory = row.Value;
                    break;
                case ServerSettingKey.EmailHost:
                    destination.SmtpConfig ??= new SmtpConfigDto();
                    destination.SmtpConfig.Host = row.Value ?? string.Empty;
                    break;
                case ServerSettingKey.EmailPort:
                    destination.SmtpConfig ??= new SmtpConfigDto();
                    destination.SmtpConfig.Port = string.IsNullOrEmpty(row.Value) ? 0 : int.Parse(row.Value, CultureInfo.InvariantCulture);
                    break;
                case ServerSettingKey.EmailAuthPassword:
                    destination.SmtpConfig ??= new SmtpConfigDto();
                    destination.SmtpConfig.Password = row.Value;
                    break;
                case ServerSettingKey.EmailAuthUserName:
                    destination.SmtpConfig ??= new SmtpConfigDto();
                    destination.SmtpConfig.UserName = row.Value;
                    break;
                case ServerSettingKey.EmailSenderAddress:
                    destination.SmtpConfig ??= new SmtpConfigDto();
                    destination.SmtpConfig.SenderAddress = row.Value;
                    break;
                case ServerSettingKey.EmailSenderDisplayName:
                    destination.SmtpConfig ??= new SmtpConfigDto();
                    destination.SmtpConfig.SenderDisplayName = row.Value;
                    break;
                case ServerSettingKey.EmailEnableSsl:
                    destination.SmtpConfig ??= new SmtpConfigDto();
                    destination.SmtpConfig.EnableSsl = bool.Parse(row.Value);
                    break;
                case ServerSettingKey.EmailSizeLimit:
                    destination.SmtpConfig ??= new SmtpConfigDto();
                    destination.SmtpConfig.SizeLimit = int.Parse(row.Value, CultureInfo.InvariantCulture);
                    break;
                case ServerSettingKey.EmailCustomizedTemplates:
                    destination.SmtpConfig ??= new SmtpConfigDto();
                    destination.SmtpConfig.CustomizedTemplates = bool.Parse(row.Value);
                    break;
                case ServerSettingKey.FirstInstallDate:
                    destination.FirstInstallDate = DateTime.Parse(row.Value, CultureInfo.InvariantCulture);
                    break;
                case ServerSettingKey.FirstInstallVersion:
                    destination.FirstInstallVersion = row.Value;
                    break;
                case ServerSettingKey.LicenseKey:
                case ServerSettingKey.EnableAuthentication:
                case ServerSettingKey.EmailServiceUrl:
                case ServerSettingKey.ConvertBookmarkToWebP:
                case ServerSettingKey.ConvertCoverToWebP:
                default:
                    break;
            }
        }

        return destination;
    }
}
