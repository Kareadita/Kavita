using System.Collections.Generic;
using API.DTOs;
using API.Entities;
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

                }
            }

            return destination;
        }
    }
}