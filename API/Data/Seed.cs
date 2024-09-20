﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using API.Constants;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.Font;
using API.Entities.Enums.Theme;
using API.Extensions;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
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

    public static readonly ImmutableArray<EpubFont> DefaultFonts =
    [
        ..new List<EpubFont>
        {
            new ()
            {
                Name = "Default",
                NormalizedName = Parser.Normalize("Default"),
                Provider = FontProvider.System,
                FileName = string.Empty,
            },
            new ()
            {
                Name = "Merriweather",
                NormalizedName = Parser.Normalize("Merriweather"),
                Provider = FontProvider.System,
                FileName = "Merriweather-Regular.woff2",
            },
            new ()
            {
                Name = "EB Garamond",
                NormalizedName = Parser.Normalize("EB Garamond"),
                Provider = FontProvider.System,
                FileName = "EBGaramond-VariableFont_wght.woff2",
            },
            new ()
            {
                Name = "Fira Sans",
                NormalizedName = Parser.Normalize("Fira Sans"),
                Provider = FontProvider.System,
                FileName = "FiraSans-Regular.woff2",
            },
            new ()
            {
                Name = "Lato",
                NormalizedName = Parser.Normalize("Lato"),
                Provider = FontProvider.System,
                FileName = "Lato-Regular.woff2",
            },
            new ()
            {
                Name = "Libre Baskerville",
                NormalizedName = Parser.Normalize("Libre Baskerville"),
                Provider = FontProvider.System,
                FileName = "LibreBaskerville-Regular.woff2",
            },
            new ()
            {
                Name = "Libre Caslon",
                NormalizedName = Parser.Normalize("Libre Caslon"),
                Provider = FontProvider.System,
                FileName = "LibreCaslonText-Regular.woff2",
            },
            new ()
            {
                Name = "Nanum Gothic",
                NormalizedName = Parser.Normalize("Nanum Gothic"),
                Provider = FontProvider.System,
                FileName = "NanumGothic-Regular.woff2",
            },
            new ()
            {
                Name = "Open Dyslexic 2",
                NormalizedName = Parser.Normalize("Open Dyslexic 2"),
                Provider = FontProvider.System,
                FileName = "OpenDyslexic-Regular.woff2",
            },
            new ()
            {
                Name = "Oswald",
                NormalizedName = Parser.Normalize("Oswald"),
                Provider = FontProvider.System,
                FileName = "Oswald-VariableFont_wght.woff2",
            },
            new ()
            {
                Name = "RocknRoll One",
                NormalizedName = Parser.Normalize("RocknRoll One"),
                Provider = FontProvider.System,
                FileName = "RocknRollOne-Regular.woff2",
            },
            new ()
            {
                Name = "Spartan",
                NormalizedName = Parser.Normalize("Spartan"),
                Provider = FontProvider.System,
                FileName = "Spartan-VariableFont_wght.woff2",
            },
        }
    ];

    public static readonly ImmutableArray<SiteTheme> DefaultThemes = [
        ..new List<SiteTheme>
        {
            new()
            {
                Name = "Dark",
                NormalizedName = "Dark".ToNormalized(),
                Provider = ThemeProvider.System,
                FileName = "dark.scss",
                IsDefault = true,
                Description = "Default theme shipped with Kavita"
            }
        }.ToArray()
    ];

    public static readonly ImmutableArray<AppUserDashboardStream> DefaultStreams = ImmutableArray.Create(
        new List<AppUserDashboardStream>
        {
            new()
            {
                Name = "on-deck",
                StreamType = DashboardStreamType.OnDeck,
                Order = 0,
                IsProvided = true,
                Visible = true
            },
            new()
            {
                Name = "recently-updated",
                StreamType = DashboardStreamType.RecentlyUpdated,
                Order = 1,
                IsProvided = true,
                Visible = true
            },
            new()
            {
                Name = "newly-added",
                StreamType = DashboardStreamType.NewlyAdded,
                Order = 2,
                IsProvided = true,
                Visible = true
            },
            new()
            {
                Name = "more-in-genre",
                StreamType = DashboardStreamType.MoreInGenre,
                Order = 3,
                IsProvided = true,
                Visible = false
            },
        }.ToArray());

    public static readonly ImmutableArray<AppUserSideNavStream> DefaultSideNavStreams = ImmutableArray.Create(
        new AppUserSideNavStream()
    {
        Name = "want-to-read",
        StreamType = SideNavStreamType.WantToRead,
        Order = 1,
        IsProvided = true,
        Visible = true
    }, new AppUserSideNavStream()
    {
        Name = "collections",
        StreamType = SideNavStreamType.Collections,
        Order = 2,
        IsProvided = true,
        Visible = true
    }, new AppUserSideNavStream()
    {
        Name = "reading-lists",
        StreamType = SideNavStreamType.ReadingLists,
        Order = 3,
        IsProvided = true,
        Visible = true
    }, new AppUserSideNavStream()
    {
        Name = "bookmarks",
        StreamType = SideNavStreamType.Bookmarks,
        Order = 4,
        IsProvided = true,
        Visible = true
    }, new AppUserSideNavStream()
    {
        Name = "all-series",
        StreamType = SideNavStreamType.AllSeries,
        Order = 5,
        IsProvided = true,
        Visible = true
    });


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
            var existing = await context.SiteTheme.FirstOrDefaultAsync(s => s.Name.Equals(theme.Name));
            if (existing == null)
            {
                await context.SiteTheme.AddAsync(theme);
            }
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedFonts(DataContext context)
    {
        await context.Database.EnsureCreatedAsync();

        foreach (var font in DefaultFonts)
        {
            var existing = await context.EpubFont.FirstOrDefaultAsync(f => f.Name.Equals(font.Name));
            if (existing == null)
            {
                await context.EpubFont.AddAsync(font);
            }
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedDefaultStreams(IUnitOfWork unitOfWork)
    {
        var allUsers = await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.DashboardStreams);
        foreach (var user in allUsers)
        {
            if (user.DashboardStreams.Count != 0) continue;
            user.DashboardStreams ??= new List<AppUserDashboardStream>();
            foreach (var defaultStream in DefaultStreams)
            {
                var newStream = new AppUserDashboardStream
                {
                    Name = defaultStream.Name,
                    IsProvided = defaultStream.IsProvided,
                    Order = defaultStream.Order,
                    StreamType = defaultStream.StreamType,
                    Visible = defaultStream.Visible,
                };

                user.DashboardStreams.Add(newStream);
            }
            unitOfWork.UserRepository.Update(user);
            await unitOfWork.CommitAsync();
        }
    }

    public static async Task SeedDefaultSideNavStreams(IUnitOfWork unitOfWork)
    {
        var allUsers = await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.SideNavStreams);
        foreach (var user in allUsers)
        {
            if (user.SideNavStreams.Count != 0) continue;
            user.SideNavStreams ??= new List<AppUserSideNavStream>();
            foreach (var defaultStream in DefaultSideNavStreams)
            {
                var newStream = new AppUserSideNavStream()
                {
                    Name = defaultStream.Name,
                    IsProvided = defaultStream.IsProvided,
                    Order = defaultStream.Order,
                    StreamType = defaultStream.StreamType,
                    Visible = defaultStream.Visible,
                };

                user.SideNavStreams.Add(newStream);
            }
            unitOfWork.UserRepository.Update(user);
            await unitOfWork.CommitAsync();
        }
    }

    public static async Task SeedSettings(DataContext context, IDirectoryService directoryService)
    {
        await context.Database.EnsureCreatedAsync();
        DefaultSettings = ImmutableArray.Create(new List<ServerSetting>()
        {
            new() {Key = ServerSettingKey.CacheDirectory, Value = directoryService.CacheDirectory},
            new() {Key = ServerSettingKey.TaskScan, Value = "daily"},
            new() {Key = ServerSettingKey.TaskBackup, Value = "daily"},
            new() {Key = ServerSettingKey.TaskCleanup, Value = "daily"},
            new() {Key = ServerSettingKey.LoggingLevel, Value = "Debug"},
            new()
            {
                Key = ServerSettingKey.BackupDirectory, Value = Path.GetFullPath(DirectoryService.BackupDirectory)
            },
            new()
            {
                Key = ServerSettingKey.Port, Value = Configuration.DefaultHttpPort + string.Empty
            }, // Not used from DB, but DB is sync with appSettings.json
            new() {
                Key = ServerSettingKey.IpAddresses, Value = Configuration.DefaultIpAddresses
            }, // Not used from DB, but DB is sync with appSettings.json
            new() {Key = ServerSettingKey.AllowStatCollection, Value = "true"},
            new() {Key = ServerSettingKey.EnableOpds, Value = "true"},
            new() {Key = ServerSettingKey.BaseUrl, Value = "/"},
            new() {Key = ServerSettingKey.InstallId, Value = HashUtil.AnonymousToken()},
            new() {Key = ServerSettingKey.InstallVersion, Value = BuildInfo.Version.ToString()},
            new() {Key = ServerSettingKey.BookmarkDirectory, Value = directoryService.BookmarkDirectory},
            new() {Key = ServerSettingKey.TotalBackups, Value = "30"},
            new() {Key = ServerSettingKey.TotalLogs, Value = "30"},
            new() {Key = ServerSettingKey.EnableFolderWatching, Value = "false"},
            new() {Key = ServerSettingKey.HostName, Value = string.Empty},
            new() {Key = ServerSettingKey.EncodeMediaAs, Value = EncodeFormat.PNG.ToString()},
            new() {Key = ServerSettingKey.LicenseKey, Value = string.Empty},
            new() {Key = ServerSettingKey.OnDeckProgressDays, Value = "30"},
            new() {Key = ServerSettingKey.OnDeckUpdateDays, Value = "7"},
            new() {Key = ServerSettingKey.CoverImageSize, Value = CoverImageSize.Default.ToString()},
            new() {
                Key = ServerSettingKey.CacheSize, Value = Configuration.DefaultCacheMemory + string.Empty
            }, // Not used from DB, but DB is sync with appSettings.json

            new() {Key = ServerSettingKey.EmailHost, Value = string.Empty},
            new() {Key = ServerSettingKey.EmailPort, Value = string.Empty},
            new() {Key = ServerSettingKey.EmailAuthPassword, Value = string.Empty},
            new() {Key = ServerSettingKey.EmailAuthUserName, Value = string.Empty},
            new() {Key = ServerSettingKey.EmailSenderAddress, Value = string.Empty},
            new() {Key = ServerSettingKey.EmailSenderDisplayName, Value = string.Empty},
            new() {Key = ServerSettingKey.EmailEnableSsl, Value = "true"},
            new() {Key = ServerSettingKey.EmailSizeLimit, Value = 26_214_400 + string.Empty},
            new() {Key = ServerSettingKey.EmailCustomizedTemplates, Value = "false"},
            new() {Key = ServerSettingKey.FirstInstallVersion, Value = BuildInfo.Version.ToString()},
            new() {Key = ServerSettingKey.FirstInstallDate, Value = DateTime.UtcNow.ToString()},

        }.ToArray());

        foreach (var defaultSetting in DefaultSettings)
        {
            var existing = await context.ServerSetting.FirstOrDefaultAsync(s => s.Key == defaultSetting.Key);
            if (existing == null)
            {
                await context.ServerSetting.AddAsync(defaultSetting);
            }
        }

        await context.SaveChangesAsync();

        // Port, IpAddresses and LoggingLevel are managed in appSettings.json. Update the DB values to match
        (await context.ServerSetting.FirstAsync(s => s.Key == ServerSettingKey.Port)).Value =
            Configuration.Port + string.Empty;
        (await context.ServerSetting.FirstAsync(s => s.Key == ServerSettingKey.IpAddresses)).Value =
            Configuration.IpAddresses;
        (await context.ServerSetting.FirstAsync(s => s.Key == ServerSettingKey.CacheDirectory)).Value =
            directoryService.CacheDirectory + string.Empty;
        (await context.ServerSetting.FirstAsync(s => s.Key == ServerSettingKey.BackupDirectory)).Value =
            DirectoryService.BackupDirectory + string.Empty;
        (await context.ServerSetting.FirstAsync(s => s.Key == ServerSettingKey.CacheSize)).Value =
            Configuration.CacheSize + string.Empty;
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
