using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.DTOs.Theme;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;

namespace API.DTOs;
#nullable enable

public sealed record UserPreferencesDto
{

    /// <summary>
    /// UI Site Global Setting: The UI theme the user should use.
    /// </summary>
    /// <remarks>Should default to Dark</remarks>
    [Required]
    public SiteThemeDto? Theme { get; set; }

    public PageLayoutMode GlobalPageLayoutMode { get; set; } = PageLayoutMode.Cards;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BlurUnreadSummaries"/>
    [Required]
    public bool BlurUnreadSummaries { get; set; } = false;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.PromptForDownloadSize"/>
    [Required]
    public bool PromptForDownloadSize { get; set; } = true;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.NoTransitions"/>
    [Required]
    public bool NoTransitions { get; set; } = false;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.CollapseSeriesRelationships"/>
    [Required]
    public bool CollapseSeriesRelationships { get; set; } = false;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.Locale"/>
    [Required]
    public string Locale { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.ColorScapeEnabled"/>
    [Required]
    public bool ColorScapeEnabled { get; set; } = true;

    /// <inheritdoc cref="API.Entities.AppUserPreferences.AniListScrobblingEnabled"/>
    public bool AniListScrobblingEnabled { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.WantToReadSync"/>
    public bool WantToReadSync { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BookReaderHighlightSlots"/>
    [Required]
    public List<HighlightSlot> BookReaderHighlightSlots { get; set; }

    #region Social

    /// <inheritdoc cref="AppUserPreferences.SocialPreferences"/>
    public AppUserSocialPreferences SocialPreferences { get; set; } = new();

    #endregion
}
