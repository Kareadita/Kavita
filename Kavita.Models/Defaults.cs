using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Font;
using Kavita.Models.Entities.Enums.User;
using Kavita.Models.Entities.User;

namespace Kavita.Models;

public static class Defaults
{
    public const string DefaultFont = "Default";

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
            Name = DefaultFont,
            NormalizedName = DefaultFont.ToNormalized(),
            Provider = FontProvider.System,
            FileName = string.Empty,
        },
        new ()
        {
            Name = "Merriweather",
            NormalizedName = "Merriweather".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Merriweather-Regular.woff2",
        },
        new ()
        {
            Name = "EB Garamond",
            NormalizedName = "EB Garamond".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "EBGaramond-VariableFont_wght.woff2",
        },
        new ()
        {
            Name = "Fira Sans",
            NormalizedName = "Fira Sans".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "FiraSans-Regular.woff2",
        },
        new ()
        {
            Name = "Lato",
            NormalizedName = "Lato".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Lato-Regular.woff2",
        },
        new ()
        {
            Name = "Libre Baskerville",
            NormalizedName = "Libre Baskerville".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "LibreBaskerville-Regular.woff2",
        },
        new ()
        {
            Name = "Nanum Gothic",
            NormalizedName = ("Nanum Gothic").ToNormalized(),
            Provider = FontProvider.System,
            FileName = "NanumGothic-Regular.woff2",
        },
        new ()
        {
            Name = "Open Dyslexic",
            NormalizedName = ("Open Dyslexic").ToNormalized(),
            Provider = FontProvider.System,
            FileName = "OpenDyslexic-Regular.woff2",
        },
        new ()
        {
            Name = "RocknRoll One",
            NormalizedName = ("RocknRoll One").ToNormalized(),
            Provider = FontProvider.System,
            FileName = "RocknRollOne-Regular.woff2",
        },
        new ()
        {
            Name = "Fast Font Serif",
            NormalizedName = ("Fast Font Serif").ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Fast_Serif.woff2",
        },
        new ()
        {
            Name = "Fast Font Sans",
            NormalizedName = ("Fast Font Sans").ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Fast_Sans.woff2",
        }
    ];

    public static readonly ImmutableArray<SiteTheme> DefaultThemes = [
        ..new List<SiteTheme>
        {
            SiteTheme.DefaultTheme,
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
            }
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
