using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.Theme;
using API.Extensions;
using API.Services;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public static class Seed
{
    /// <summary>
    /// Generated on Startup. Seed.SeedSettings must run before
    /// </summary>
    public static ImmutableArray<ServerSetting> DefaultSettings;

    public static readonly ImmutableArray<SiteTheme> DefaultThemes = ImmutableArray.Create(
        new List<SiteTheme>
        {
            new()
            {
                Name = "Dark",
                NormalizedName = "Dark".ToNormalized(),
                Provider = ThemeProvider.System,
                FileName = "dark.scss",
                IsDefault = true,
            }
        }.ToArray());

    public static async Task SeedRoles(RoleManager<AppRole> roleManager)
    {
        var roles = typeof(PolicyConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .ToDictionary(f => f.Name,
                f => (string) f.GetValue(null)!).Values
            .Select(policyName => new AppRole() {Name = policyName})
            .ToList();

        foreach (var role in roles)
        {
            var exists = await roleManager.RoleExistsAsync(role.Name!);
            if (!exists)
            {
                await roleManager.CreateAsync(role);
            }
        }
    }

    public static async Task SeedThemes(DataContext context)
    {
        await context.Database.EnsureCreatedAsync();

        foreach (var theme in DefaultThemes)
        {
            var existing = context.SiteTheme.FirstOrDefault(s => s.Name.Equals(theme.Name));
            if (existing == null)
            {
                await context.SiteTheme.AddAsync(theme);
            }
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedSettings(DataContext context, IDirectoryService directoryService)
    {
        await context.Database.EnsureCreatedAsync();
        DefaultSettings = ImmutableArray.Create(new List<ServerSetting>()
        {
            new() {Key = ServerSettingKey.CacheDirectory, Value = directoryService.CacheDirectory},
            new() {Key = ServerSettingKey.TaskScan, Value = "daily"},
            new() {Key = ServerSettingKey.LoggingLevel, Value = "Debug"},
            new() {Key = ServerSettingKey.TaskBackup, Value = "daily"},
            new()
            {
                Key = ServerSettingKey.BackupDirectory, Value = Path.GetFullPath(DirectoryService.BackupDirectory)
            },
            new()
            {
                Key = ServerSettingKey.Port, Value = "5000"
            }, // Not used from DB, but DB is sync with appSettings.json
            new() {Key = ServerSettingKey.AllowStatCollection, Value = "true"},
            new() {Key = ServerSettingKey.EnableOpds, Value = "false"},
            new() {Key = ServerSettingKey.EnableAuthentication, Value = "true"},
            new() {Key = ServerSettingKey.BaseUrl, Value = "/"},
            new() {Key = ServerSettingKey.InstallId, Value = HashUtil.AnonymousToken()},
            new() {Key = ServerSettingKey.InstallVersion, Value = BuildInfo.Version.ToString()},
            new() {Key = ServerSettingKey.BookmarkDirectory, Value = directoryService.BookmarkDirectory},
            new() {Key = ServerSettingKey.EmailServiceUrl, Value = EmailService.DefaultApiUrl},
            new() {Key = ServerSettingKey.ConvertBookmarkToWebP, Value = "false"},
            new() {Key = ServerSettingKey.EnableSwaggerUi, Value = "false"},
            new() {Key = ServerSettingKey.TotalBackups, Value = "30"},
            new() {Key = ServerSettingKey.TotalLogs, Value = "30"},
            new() {Key = ServerSettingKey.EnableFolderWatching, Value = "false"},
            new() {Key = ServerSettingKey.ConvertCoverToWebP, Value = "false"},
        }.ToArray());

        foreach (var defaultSetting in DefaultSettings)
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
        context.ServerSetting.First(s => s.Key == ServerSettingKey.CacheDirectory).Value =
            directoryService.CacheDirectory + string.Empty;
        context.ServerSetting.First(s => s.Key == ServerSettingKey.BackupDirectory).Value =
            DirectoryService.BackupDirectory + string.Empty;

        await context.SaveChangesAsync();

    }

    public static async Task SeedUserApiKeys(DataContext context)
    {
        await context.Database.EnsureCreatedAsync();

        var users = await context.AppUser.ToListAsync();
        foreach (var user in users.Where(user => string.IsNullOrEmpty(user.ApiKey)))
        {
            user.ApiKey = HashUtil.ApiKey();
        }
        await context.SaveChangesAsync();
    }
}
