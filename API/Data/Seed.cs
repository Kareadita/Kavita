using System.Collections.Generic;
using System.IO;
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
            IList<ServerSetting> defaultSettings = new List<ServerSetting>()
            {
                new ServerSetting() {Key = "CacheDirectory", Value = CacheService.CacheDirectory}
            };

            await context.ServerSetting.AddRangeAsync(defaultSettings);
            await context.SaveChangesAsync();
            // await context.ServerSetting.AddAsync(new ServerSetting
            // {
            //     CacheDirectory = CacheService.CacheDirectory
            // });
            //
            // await context.SaveChangesAsync();
        }
    }
}