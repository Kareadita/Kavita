using System.ComponentModel.DataAnnotations;
using API.DTOs.Theme;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;

namespace API.DTOs;
#nullable enable

public sealed record UserPreferencesDto
{
    /// <inheritdoc cref="API.Entities.AppUserPreferences.ReadingDirection"/>
    [Required]
    public ReadingDirection ReadingDirection { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.ScalingOption"/>
    [Required]
    public ScalingOption ScalingOption { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.PageSplitOption"/>
    [Required]
    public PageSplitOption PageSplitOption { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.ReaderMode"/>
    [Required]
    public ReaderMode ReaderMode { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.LayoutMode"/>
    [Required]
    public LayoutMode LayoutMode { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.EmulateBook"/>
    [Required]
    public bool EmulateBook { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BackgroundColor"/>
    [Required]
    public string BackgroundColor { get; set; } = "#000000";
    /// <inheritdoc cref="API.Entities.AppUserPreferences.SwipeToPaginate"/>
    [Required]
    public bool SwipeToPaginate { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.AutoCloseMenu"/>
    [Required]
    public bool AutoCloseMenu { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.ShowScreenHints"/>
    [Required]
    public bool ShowScreenHints { get; set; } = true;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.AllowAutomaticWebtoonReaderDetection"/>
    [Required]
    public bool AllowAutomaticWebtoonReaderDetection { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserPreferences.BookReaderMargin"/>
    [Required]
    public int BookReaderMargin { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BookReaderLineSpacing"/>
    [Required]
    public int BookReaderLineSpacing { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BookReaderFontSize"/>
    [Required]
    public int BookReaderFontSize { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BookReaderFontFamily"/>
    [Required]
    public string BookReaderFontFamily { get; set; } = null!;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BookReaderTapToPaginate"/>
    [Required]
    public bool BookReaderTapToPaginate { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BookReaderReadingDirection"/>
    [Required]
    public ReadingDirection BookReaderReadingDirection { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BookReaderWritingStyle"/>
    [Required]
    public WritingStyle BookReaderWritingStyle { get; set; }

    /// <summary>
    /// UI Site Global Setting: The UI theme the user should use.
    /// </summary>
    /// <remarks>Should default to Dark</remarks>
    [Required]
    public SiteThemeDto? Theme { get; set; }

    [Required] public string BookReaderThemeName { get; set; } = null!;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BookReaderLayoutMode"/>
    [Required]
    public BookPageLayoutMode BookReaderLayoutMode { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.BookReaderImmersiveMode"/>
    [Required]
    public bool BookReaderImmersiveMode { get; set; } = false;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.GlobalPageLayoutMode"/>
    [Required]
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
    /// <inheritdoc cref="API.Entities.AppUserPreferences.ShareReviews"/>
    [Required]
    public bool ShareReviews { get; set; } = false;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.Locale"/>
    [Required]
    public string Locale { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserPreferences.PdfTheme"/>
    [Required]
    public PdfTheme PdfTheme { get; set; } = PdfTheme.Dark;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.PdfScrollMode"/>
    [Required]
    public PdfScrollMode PdfScrollMode { get; set; } = PdfScrollMode.Vertical;
    /// <inheritdoc cref="API.Entities.AppUserPreferences.PdfSpreadMode"/>
    [Required]
    public PdfSpreadMode PdfSpreadMode { get; set; } = PdfSpreadMode.None;

    /// <inheritdoc cref="API.Entities.AppUserPreferences.AniListScrobblingEnabled"/>
    public bool AniListScrobblingEnabled { get; set; }
    /// <inheritdoc cref="API.Entities.AppUserPreferences.WantToReadSync"/>
    public bool WantToReadSync { get; set; }
}
