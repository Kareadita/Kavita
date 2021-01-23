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
            destination = new ServerSettingDto();
            foreach (var row in source)
            {
                switch (row.Key)
                {
                    case "CacheDirectory":
                        destination.CacheDirectory = row.Value;
                        break;
                    default:
                        break;
                }
            }

            return destination;
        }
    }
}