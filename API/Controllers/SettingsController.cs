using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    [Authorize]
    public class SettingsController : BaseApiController
    {
        private readonly DataContext _dataContext;
        private readonly ILogger<SettingsController> _logger;
        private readonly IMapper _mapper;
        private readonly ITaskScheduler _taskScheduler;

        public SettingsController(DataContext dataContext, ILogger<SettingsController> logger, IMapper mapper, ITaskScheduler taskScheduler)
        {
            _dataContext = dataContext;
            _logger = logger;
            _mapper = mapper;
            _taskScheduler = taskScheduler;
        }

        [HttpGet("")]
        public async Task<ActionResult<ServerSettingDto>> GetSettings()
        {
            var settings = await _dataContext.ServerSetting.Select(x => x).ToListAsync();
            return _mapper.Map<ServerSettingDto>(settings);
        }
        
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("")]
        public async Task<ActionResult> UpdateSettings(ServerSettingDto updateSettingsDto)
        {
            _logger.LogInformation($"{User.GetUsername()}  is updating Server Settings");
            
            if (updateSettingsDto.CacheDirectory.Equals(string.Empty))
            {
                return BadRequest("Cache Directory cannot be empty");
            }

            if (!Directory.Exists(updateSettingsDto.CacheDirectory))
            {
                return BadRequest("Directory does not exist or is not accessible.");
            }
            // TODO: Figure out how to handle a change. This means that on clean, we need to clean up old cache 
            // directory and new one, but what if someone is reading? 
            // I can just clean both always, /cache/ is an owned folder, so users shouldn't use it. 
            
            
            //_dataContext.ServerSetting.Update
            return BadRequest("Not Implemented");
        }
    }
}