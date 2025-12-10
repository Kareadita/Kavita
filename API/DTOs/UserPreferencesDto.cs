using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.DTOs.Theme;
using API.Entities;
using API.Entities.Enums.UserPreferences;
using API.Entities.User;

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
    /// <inheritdoc cref="AppUserPreferences.BlurUnreadSummaries"/>
    [Required]
    public bool BlurUnreadSummaries { get; set; } = false;
    /// <inheritdoc cref="AppUserPreferences.PromptForDownloadSize"/>
    [Required]
    public bool PromptForDownloadSize { get; set; } = true;
    /// <inheritdoc cref="AppUserPreferences.NoTransitions"/>
    [Required]
    public bool NoTransitions { get; set; } = false;
    /// <inheritdoc cref="AppUserPreferences.CollapseSeriesRelationships"/>
    [Required]
    public bool CollapseSeriesRelationships { get; set; } = false;
    /// <inheritdoc cref="AppUserPreferences.Locale"/>
    [Required]
    public string Locale { get; set; }
    /// <inheritdoc cref="AppUserPreferences.ColorScapeEnabled"/>
    [Required]
    public bool ColorScapeEnabled { get; set; } = true;
    /// <inheritdoc cref="AppUserPreferences.DataSaver"/>
    [Required]
    public bool DataSaver { get; set; } = false;
    /// <inheritdoc cref="AppUserPreferences.PromptForRereadsAfter"/>
    [Required]
    public int PromptForRereadsAfter { get; set; }
    /// <inheritdoc cref="AppUserPreferences.CustomKeyBinds"/>
    [Required]
    public Dictionary<KeyBindTarget, IList<KeyBind>> CustomKeyBinds { get; set; } = [];

    /// <inheritdoc cref="AppUserPreferences.AniListScrobblingEnabled"/>
    public bool AniListScrobblingEnabled { get; set; }
    /// <inheritdoc cref="AppUserPreferences.WantToReadSync"/>
    public bool WantToReadSync { get; set; }
    /// <inheritdoc cref="AppUserPreferences.BookReaderHighlightSlots"/>
    [Required]
    public List<HighlightSlot> BookReaderHighlightSlots { get; set; }

    #region Social

    /// <inheritdoc cref="AppUserPreferences.SocialPreferences"/>
    [Required]
    public AppUserSocialPreferences SocialPreferences { get; set; } = new();

    #endregion

    /// <inheritdoc cref="AppUserPreferences.OpdsPreferences"/>
    [Required]
    public AppUserOpdsPreferences OpdsPreferences { get; set; } = new();
}
