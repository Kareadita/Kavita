using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Entities;
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
            IList<ServerSetting> defaultSettings = new List<ServerSetting>()
            {
                new() {Key = "CacheDirectory", Value = CacheService.CacheDirectory}
            };
            var settings = await context.ServerSetting.Select(s => s).ToListAsync();
            foreach (var defaultSetting in defaultSettings)
            {
                var existing = settings.SingleOrDefault(s => s.Key == defaultSetting.Key);
                if (existing == null)
                {
                    settings.Add(defaultSetting);
                }
            }
            
            await context.SaveChangesAsync();
        }
    }
}