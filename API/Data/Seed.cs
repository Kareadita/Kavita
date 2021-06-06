using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using API.Entities.Enums;
using API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public static class Seed
    {
        public static async Task SeedRoles(RoleManager<AppRole> roleManager)
        {
            var roles = new List<AppRole>
            {
                new() {Name = PolicyConstants.AdminRole},
                new() {Name = PolicyConstants.PlebRole}
            };

            foreach (var role in roles)
            {
                var exists = await roleManager.RoleExistsAsync(role.Name);
                if (!exists)
                {
                    await roleManager.CreateAsync(role);      
                }
            }
        }

        public static async Task SeedSettings(DataContext context)
        {
            await context.Database.EnsureCreatedAsync();
            
            IList<ServerSetting> defaultSettings = new List<ServerSetting>()
            {
                new() {Key = ServerSettingKey.CacheDirectory, Value = CacheService.CacheDirectory},
                new () {Key = ServerSettingKey.TaskScan, Value = "daily"},
                //new () {Key = ServerSettingKey.LoggingLevel, Value = "Information"},
                new () {Key = ServerSettingKey.TaskBackup, Value = "weekly"},
                new () {Key = ServerSettingKey.BackupDirectory, Value = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "backups/"))},
                new () {Key = ServerSettingKey.Port, Value = "5000"},
            };
            
            foreach (var defaultSetting in defaultSettings)
            {
                var existing = context.ServerSetting.FirstOrDefault(s => s.Key == defaultSetting.Key);
                if (existing == null)
                {
                    await context.ServerSetting.AddAsync(defaultSetting);
                }
            }

            await context.SaveChangesAsync();
        }

        public static async Task SeedSeriesMetadata(DataContext context)
        {
            await context.Database.EnsureCreatedAsync();
            
            context.Database.EnsureCreated();
            var series = await context.Series
                .Include(s => s.Metadata).ToListAsync();
                
            foreach (var s in series)
            {
                s.Metadata ??= new SeriesMetadata();
            }

            await context.SaveChangesAsync();
        }
    }
}