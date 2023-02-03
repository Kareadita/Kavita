using System.Collections.Generic;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using AutoMapper;

namespace API.Helpers.Converters;

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
                case ServerSettingKey.LoggingLevel:
                    destination.LoggingLevel = row.Value;
                    break;
                case ServerSettingKey.TaskBackup:
                    destination.TaskBackup = row.Value;
                    break;
                case ServerSettingKey.Port:
                    destination.Port = int.Parse(row.Value);
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
                case ServerSettingKey.EmailServiceUrl:
                    destination.EmailServiceUrl = row.Value;
                    break;
                case ServerSettingKey.InstallVersion:
                    destination.InstallVersion = row.Value;
                    break;
                case ServerSettingKey.ConvertBookmarkToWebP:
                    destination.ConvertBookmarkToWebP = bool.Parse(row.Value);
                    break;
                case ServerSettingKey.ConvertCoverToWebP:
                    destination.ConvertCoverToWebP = bool.Parse(row.Value);
                    break;
                case ServerSettingKey.TotalBackups:
                    destination.TotalBackups = int.Parse(row.Value);
                    break;
                case ServerSettingKey.InstallId:
                    destination.InstallId = row.Value;
                    break;
                case ServerSettingKey.EnableFolderWatching:
                    destination.EnableFolderWatching = bool.Parse(row.Value);
                    break;
                case ServerSettingKey.TotalLogs:
                    destination.TotalLogs = int.Parse(row.Value);
                    break;
                case ServerSettingKey.HostName:
                    destination.HostName = row.Value;
                    break;
            }
        }

        return destination;
    }
}
