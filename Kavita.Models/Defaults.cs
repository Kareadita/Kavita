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
            Family = DefaultFont,
            Name = DefaultFont,
            NormalizedName = DefaultFont.ToNormalized(),
            Provider = FontProvider.System,
            FileName = string.Empty,
            Style = "normal",
            Weight = "400",
        },
        new ()
        {
            Family = "Merriweather",
            Name = "Merriweather",
            NormalizedName = "Merriweather".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Merriweather-Regular.woff2",
            Style = "normal",
            Weight = "400",
        },
        new ()
        {
            Family = "Merriweather",
            Name = "Merriweather Italic",
            NormalizedName = "Merriweather-Italic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Merriweather-Italic.woff2",
            Style = "italic",
            Weight = "400",
        },
        new ()
        {
            Family = "Merriweather",
            Name = "Merriweather Bold",
            NormalizedName = "Merriweather-Bold".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Merriweather-Bold.woff2",
            Style = "normal",
            Weight = "700",
        },
        new ()
        {
            Family = "Merriweather",
            Name = "Merriweather BoldItalic",
            NormalizedName = "Merriweather-BoldItalic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Merriweather-BoldItalic.woff2",
            Style = "italic",
            Weight = "700",
        },
        new ()
        {
            Family = "EB Garamond",
            Name = "EB Garamond",
            NormalizedName = "EB Garamond".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "EBGaramond-VariableFont_wght.woff2",
            Style = "normal",
            Weight = "400 800",
        },
        new ()
        {
            Family = "EB Garamond",
            Name = "EB Garamond Italic VariableFont",
            NormalizedName = "EBGaramond-Italic-VariableFont".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "EBGaramond-Italic-VariableFont_wght.woff2",
            Style = "italic",
            Weight = "400 800",
        },
        new ()
        {
            Family = "Fira Sans",
            Name = "Fira Sans",
            NormalizedName = "Fira Sans".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "FiraSans-Regular.woff2",
            Style = "normal",
            Weight = "400",
        },
        new ()
        {
            Family = "Fira Sans",
            Name = "Fira Sans Italic",
            NormalizedName = "FiraSans-Italic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "FiraSans-Italic.woff2",
            Style = "italic",
            Weight = "400",
        },
        new ()
        {
            Family = "Fira Sans",
            Name = "Fira Sans Bold",
            NormalizedName = "FiraSans-Bold".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "FiraSans-Bold.woff2",
            Style = "normal",
            Weight = "700",
        },
        new ()
        {
            Family = "Fira Sans",
            Name = "Fira Sans BoldItalic",
            NormalizedName = "FiraSans-BoldItalic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "FiraSans-BoldItalic.woff2",
            Style = "italic",
            Weight = "700",
        },
        new ()
        {
            Family = "Lato",
            Name = "Lato",
            NormalizedName = "Lato".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Lato-Regular.woff2",
            Style = "normal",
            Weight = "400",
        },
        new ()
        {
            Family = "Lato",
            Name = "Lato Italic",
            NormalizedName = "Lato-Italic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Lato-Italic.woff2",
            Style = "italic",
            Weight = "400",
        },
        new ()
        {
            Family = "Lato",
            Name = "Lato Bold",
            NormalizedName = "Lato-Bold".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Lato-Bold.woff2",
            Style = "normal",
            Weight = "700",
        },
        new ()
        {
            Family = "Lato",
            Name = "Lato BoldItalic",
            NormalizedName = "Lato-BoldItalic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Lato-BoldItalic.woff2",
            Style = "italic",
            Weight = "700",
        },
        new ()
        {
            Family = "Libre Baskerville",
            Name = "Libre Baskerville",
            NormalizedName = "Libre Baskerville".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "LibreBaskerville-Regular.woff2",
            Style = "normal",
            Weight = "400",
        },
        new ()
        {
            Family = "Libre Baskerville",
            Name = "Libre Baskerville Italic",
            NormalizedName = "LibreBaskerville-Italic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "LibreBaskerville-Italic.woff2",
            Style = "italic",
            Weight = "400",
        },
        new ()
        {
            Family = "Libre Baskerville",
            Name = "Libre Baskerville Bold",
            NormalizedName = "LibreBaskerville-Bold".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "LibreBaskerville-Bold.woff2",
            Style = "normal",
            Weight = "700",
        },
        new ()
        {
            Family = "Nanum Gothic",
            Name = "Nanum Gothic",
            NormalizedName = "Nanum Gothic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "NanumGothic-Regular.woff2",
            Style = "normal",
            Weight = "400",
        },
        new ()
        {
            Family = "Nanum Gothic",
            Name = "Nanum Gothic Bold",
            NormalizedName = "NanumGothic-Bold".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "NanumGothic-Bold.woff2",
            Style = "normal",
            Weight = "700",
        },
        new ()
        {
            Family = "Nanum Gothic",
            Name = "Nanum Gothic ExtraBold",
            NormalizedName = "NanumGothic-ExtraBold".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "NanumGothic-ExtraBold.woff2",
            Style = "normal",
            Weight = "800",
        },
        new ()
        {
            Family = "Open Dyslexic",
            Name = "Open Dyslexic",
            NormalizedName = "Open Dyslexic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "OpenDyslexic-Regular.woff2",
            Style = "normal",
            Weight = "400",
        },
        new ()
        {
            Family = "Open Dyslexic",
            Name = "Open Dyslexic Italic",
            NormalizedName = "OpenDyslexic-Italic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "OpenDyslexic-Italic.woff2",
            Style = "italic",
            Weight = "400",
        },
        new ()
        {
            Family = "Open Dyslexic",
            Name = "Open Dyslexic Bold",
            NormalizedName = "OpenDyslexic-Bold".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "OpenDyslexic-Bold.woff2",
            Style = "normal",
            Weight = "700",
        },
        new ()
        {
            Family = "Open Dyslexic",
            Name = "Open Dyslexic BoldItalic",
            NormalizedName = "OpenDyslexic-BoldItalic".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "OpenDyslexic-BoldItalic.woff2",
            Style = "italic",
            Weight = "700",
        },
        new ()
        {
            Family = "RocknRoll One",
            Name = "RocknRoll One",
            NormalizedName = "RocknRoll One".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "RocknRollOne-Regular.woff2",
            Style = "normal",
            Weight = "400",
        },
        new ()
        {
            Family = "Fast Font Serif",
            Name = "Fast Font Serif",
            NormalizedName = "Fast Font Serif".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Fast_Serif.woff2",
            Style = "normal",
            Weight = "400",
        },
        new ()
        {
            Family = "Fast Font Sans",
            Name = "Fast Font Sans",
            NormalizedName = "Fast Font Sans".ToNormalized(),
            Provider = FontProvider.System,
            FileName = "Fast_Sans.woff2",
            Style = "normal",
            Weight = "400",
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
