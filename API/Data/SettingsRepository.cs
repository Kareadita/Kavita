using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class SettingsRepository : ISettingsRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public SettingsRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public void Update(ServerSetting settings)
        {
            _context.Entry(settings).State = EntityState.Modified;
        }

        public async Task<ServerSettingDto> GetSettingsDtoAsync()
        {
            var settings = await _context.ServerSetting
                .Select(x => x)
                .AsNoTracking()
                .ToListAsync();
            return _mapper.Map<ServerSettingDto>(settings);
        }

        public Task<ServerSetting> GetSettingAsync(ServerSettingKey key)
        {
            return _context.ServerSetting.SingleOrDefaultAsync(x => x.Key == key);
        }

        public async Task<IEnumerable<ServerSetting>> GetSettingsAsync()
        {
            return await _context.ServerSetting.ToListAsync();
        }
    }
}