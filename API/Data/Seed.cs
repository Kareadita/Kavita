using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using API.Entities.Enums;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public static class Seed
    {
        public static async Task SeedRoles(RoleManager<AppRole> roleManager)
        {
            var roles = typeof(PolicyConstants)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .ToDictionary(f => f.Name,
                    f => (string) f.GetValue(null)).Values
                .Select(policyName => new AppRole() {Name = policyName})
                .ToList();

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
                new () {Key = ServerSettingKey.CacheDirectory, Value = DirectoryService.CacheDirectory},
                new () {Key = ServerSettingKey.TaskScan, Value = "daily"},
                new () {Key = ServerSettingKey.LoggingLevel, Value = "Information"}, // Not used from DB, but DB is sync with appSettings.json
                new () {Key = ServerSettingKey.TaskBackup, Value = "weekly"},
                new () {Key = ServerSettingKey.BackupDirectory, Value = Path.GetFullPath(DirectoryService.BackupDirectory)},
                new () {Key = ServerSettingKey.Port, Value = "5000"}, // Not used from DB, but DB is sync with appSettings.json
                new () {Key = ServerSettingKey.AllowStatCollection, Value = "true"},
                new () {Key = ServerSettingKey.EnableOpds, Value = "false"},
                new () {Key = ServerSettingKey.EnableAuthentication, Value = "true"},
                new () {Key = ServerSettingKey.BaseUrl, Value = "/"},
                new () {Key = ServerSettingKey.InstallId, Value = HashUtil.AnonymousToken()},
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

            // Port and LoggingLevel are managed in appSettings.json. Update the DB values to match
            context.ServerSetting.First(s => s.Key == ServerSettingKey.Port).Value =
                Configuration.Port + string.Empty;
            context.ServerSetting.First(s => s.Key == ServerSettingKey.LoggingLevel).Value =
                Configuration.LogLevel + string.Empty;
            context.ServerSetting.First(s => s.Key == ServerSettingKey.CacheDirectory).Value =
                DirectoryService.CacheDirectory + string.Empty;
            context.ServerSetting.First(s => s.Key == ServerSettingKey.BackupDirectory).Value =
                DirectoryService.BackupDirectory + string.Empty;

            await context.SaveChangesAsync();

        }

        public static async Task SeedUserApiKeys(DataContext context)
        {
            await context.Database.EnsureCreatedAsync();

            var users = await context.AppUser.ToListAsync();
            foreach (var user in users)
            {
                if (string.IsNullOrEmpty(user.ApiKey))
                {
                    user.ApiKey = HashUtil.ApiKey();
                }
            }
            await context.SaveChangesAsync();
        }
    }
}
