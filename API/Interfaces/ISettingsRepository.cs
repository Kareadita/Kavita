using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface ISettingsRepository
    {
        void Update(ServerSetting settings);
        Task<ServerSettingDto> GetSettingsDtoAsync();
        Task<ServerSetting> GetSettingAsync(ServerSettingKey key);
        Task<IEnumerable<ServerSetting>> GetSettingsAsync();
        
    }
}