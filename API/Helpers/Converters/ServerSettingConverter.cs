﻿using System.Collections.Generic;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using AutoMapper;

namespace API.Helpers.Converters
{
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
                    case ServerSettingKey.AllowStatCollection:
                        destination.AllowStatCollection = bool.Parse(row.Value);
                        break;
                    case ServerSettingKey.EnableOpds:
                        destination.EnableOpds = bool.Parse(row.Value);
                        break;
                }
            }

            return destination;
        }
    }
}
