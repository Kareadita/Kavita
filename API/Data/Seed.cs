using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using API.Constants;
using API.Data.Repositories;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.Font;
using API.Entities.Enums.Theme;
using API.Entities.Enums.User;
using API.Entities.MetadataMatching;
using API.Entities.User;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.Services.Tasks;
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

    public static readonly ImmutableArray<HighlightSlot> DefaultHighlightSlots =
    [
        new()
        {
            Id = 1,
            SlotNumber = 0,
            Color = new RgbaColor { R = 0, G = 255, B = 255, A = 0.4f }
        },
        new()
        {
            Id = 2,
            SlotNumber = 1,
            Color = new RgbaColor { R = 0, G = 255, B = 0, A = 0.4f }
        },
        new()
        {
            Id = 3,
            SlotNumber = 2,
            Color = new RgbaColor { R = 255, G = 255, B = 0, A = 0.4f }
        },
        new()
        {
            Id = 4,
            SlotNumber = 3,
            Color = new RgbaColor { R = 255, G = 165, B = 0, A = 0.4f }
        },
        new()
        {
            Id = 5,
            SlotNumber = 4,
            Color = new RgbaColor { R = 255, G = 0, B = 255, A = 0.4f }
        }
    ];

    public static readonly ImmutableArray<EpubFont> DefaultFonts =
    [
        new ()
        {
            Name = FontService.DefaultFont,
            NormalizedName = Parser.Normalize(FontService.DefaultFont),
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
            Name = "Nanum Gothic",
            NormalizedName = Parser.Normalize("Nanum Gothic"),
            Provider = FontProvider.System,
            FileName = "NanumGothic-Regular.woff2",
        },
        new ()
        {
            Name = "Open Dyslexic",
            NormalizedName = Parser.Normalize("Open Dyslexic"),
            Provider = FontProvider.System,
            FileName = "OpenDyslexic-Regular.woff2",
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
            Name = "Fast Font Serif",
            NormalizedName = Parser.Normalize("Fast Font Serif"),
            Provider = FontProvider.System,
            FileName = "Fast_Serif.woff2",
        },
        new ()
        {
            Name = "Fast Font Sans",
            NormalizedName = Parser.Normalize("Fast Font Sans"),
            Provider = FontProvider.System,
            FileName = "Fast_Sans.woff2",
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

    public static readonly ImmutableArray<AppUserDashboardStream> DefaultStreams = [
        ..new List<AppUserDashboardStream>
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
        }.ToArray()
    ];

    public static readonly ImmutableArray<AppUserSideNavStream> DefaultSideNavStreams =
    [
        new()
    {
        Name = "want-to-read",
        StreamType = SideNavStreamType.WantToRead,
        Order = 1,
        IsProvided = true,
        Visible = true
    }, new()
    {
        Name = "collections",
        StreamType = SideNavStreamType.Collections,
        Order = 2,
        IsProvided = true,
        Visible = true
    }, new()
    {
        Name = "reading-lists",
        StreamType = SideNavStreamType.ReadingLists,
        Order = 3,
        IsProvided = true,
        Visible = true
    }, new()
    {
        Name = "bookmarks",
        StreamType = SideNavStreamType.Bookmarks,
        Order = 4,
        IsProvided = true,
        Visible = true
    }, new()
    {
        Name = "all-series",
        StreamType = SideNavStreamType.AllSeries,
        Order = 5,
        IsProvided = true,
        Visible = true
    },
    new()
    {
        Name = "browse-authors",
        StreamType = SideNavStreamType.BrowsePeople,
        Order = 6,
        IsProvided = true,
        Visible = true
    }
    ];


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
            user.DashboardStreams ??= [];
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
            user.SideNavStreams ??= [];
            foreach (var defaultStream in DefaultSideNavStreams)
            {
                if (user.SideNavStreams.Any(s => s.Name == defaultStream.Name && s.StreamType == defaultStream.StreamType)) continue;
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

    public static async Task SeedDefaultHighlightSlots(IUnitOfWork unitOfWork)
    {
        var allUsers = await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.UserPreferences);
        foreach (var user in allUsers)
        {
            if (user.UserPreferences.BookReaderHighlightSlots.Any()) break;

            user.UserPreferences.BookReaderHighlightSlots = DefaultHighlightSlots.ToList();
            unitOfWork.UserRepository.Update(user);
        }
        await unitOfWork.CommitAsync();
    }

    public static async Task SeedSettings(DataContext context, IDirectoryService directoryService)
    {
        await context.Database.EnsureCreatedAsync();
        DefaultSettings = [
            ..new List<ServerSetting>()
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
                new() {Key = ServerSettingKey.EncodeMediaAs, Value = nameof(EncodeFormat.PNG)},
                new() {Key = ServerSettingKey.LicenseKey, Value = string.Empty},
                new() {Key = ServerSettingKey.OnDeckProgressDays, Value = "30"},
                new() {Key = ServerSettingKey.OnDeckUpdateDays, Value = "7"},
                new() {Key = ServerSettingKey.CoverImageSize, Value = nameof(CoverImageSize.Default)},
                new() {Key = ServerSettingKey.PdfRenderResolution, Value = nameof(PdfRenderResolution.Default)},
                new() {
                    Key = ServerSettingKey.CacheSize, Value = Configuration.DefaultCacheMemory + string.Empty
                }, // Not used from DB, but DB is sync with appSettings.json
                new() { Key = ServerSettingKey.OidcConfiguration, Value = JsonSerializer.Serialize(new OidcConfigDto())},

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
                new() {Key = ServerSettingKey.FirstInstallDate, Value = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)},
                new() {Key = ServerSettingKey.StatsApiHits, Value = "0"},
            }.ToArray()
        ];

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

        await SetOidcSettingsFromDisk(context);


        await context.SaveChangesAsync();
    }

    public static async Task SetOidcSettingsFromDisk(DataContext context)
    {
        var oidcSettingEntry = await context.ServerSetting
            .FirstOrDefaultAsync(setting => setting.Key == ServerSettingKey.OidcConfiguration);

        var storedOidcSettings = JsonSerializer.Deserialize<OidcConfigDto>(oidcSettingEntry!.Value)!;

        var diskOidcSettings = Configuration.OidcSettings;

        storedOidcSettings.Authority = diskOidcSettings.Authority;
        storedOidcSettings.ClientId = diskOidcSettings.ClientId;
        storedOidcSettings.Secret = diskOidcSettings.Secret;
        storedOidcSettings.CustomScopes = diskOidcSettings.CustomScopes;

        oidcSettingEntry.Value = JsonSerializer.Serialize(storedOidcSettings);
    }

    public static async Task SeedMetadataSettings(DataContext context)
    {
        await context.Database.EnsureCreatedAsync();

        var existing = await context.MetadataSettings.FirstOrDefaultAsync();
        if (existing == null)
        {
            existing = new MetadataSettings()
            {
                Enabled = true,
                EnablePeople = true,
                EnableRelationships = true,
                EnableSummary = true,
                EnablePublicationStatus = true,
                EnableStartDate = true,
                EnableTags = false,
                EnableGenres = true,
                EnableLocalizedName = false,
                FirstLastPeopleNaming = true,
                EnableCoverImage = true,
                EnableChapterTitle = false,
                EnableChapterSummary = true,
                EnableChapterPublisher = true,
                EnableChapterCoverImage = false,
                EnableChapterReleaseDate = true,
                PersonRoles = [PersonRole.Writer, PersonRole.CoverArtist, PersonRole.Character]
            };
            await context.MetadataSettings.AddAsync(existing);
        }


        await context.SaveChangesAsync();

    }

    public static List<AppUserAuthKey> CreateDefaultAuthKeys()
    {
        return
        [
            new AppUserAuthKey()
            {
                Name = AuthKeyHelper.OpdsKeyName,
                Key = AuthKeyHelper.GenerateKey(32),
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = null,
                Provider = AuthKeyProvider.System,
            },
            new AppUserAuthKey()
            {
                Name = AuthKeyHelper.ImageOnlyKeyName,
                Key = AuthKeyHelper.GenerateKey(32),
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = null,
                Provider = AuthKeyProvider.System,
            }
        ];
    }
}
