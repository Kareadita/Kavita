using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using API.Services;
using Microsoft.AspNetCore.Identity;

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
            context.Database.EnsureCreated();
            
            IList<ServerSetting> defaultSettings = new List<ServerSetting>()
            {
                new() {Key = ServerSettingKey.CacheDirectory, Value = CacheService.CacheDirectory},
                new () {Key = ServerSettingKey.TaskScan, Value = "daily"}
            };
            
            foreach (var defaultSetting in defaultSettings)
            {
                var existing = context.ServerSetting.FirstOrDefault(s => s.Key == defaultSetting.Key);
                if (existing == null)
                {
                    context.ServerSetting.Add(defaultSetting);
                }
            }

            await context.SaveChangesAsync();
        }
    }
}