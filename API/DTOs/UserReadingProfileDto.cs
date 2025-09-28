using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;

namespace API.DTOs;

public sealed record UserReadingProfileDto
{

    public int Id { get; set; }
    public int UserId { get; init; }

    public string Name { get; init; }
    public ReadingProfileKind Kind { get; init; }

    #region MangaReader

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.ReadingDirection"/>
    [Required]
    public ReadingDirection ReadingDirection { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.ScalingOption"/>
    [Required]
    public ScalingOption ScalingOption { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.PageSplitOption"/>
    [Required]
    public PageSplitOption PageSplitOption { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.ReaderMode"/>
    [Required]
    public ReaderMode ReaderMode { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.AutoCloseMenu"/>
    [Required]
    public bool AutoCloseMenu { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.ShowScreenHints"/>
    [Required]
    public bool ShowScreenHints { get; set; } = true;

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.EmulateBook"/>
    [Required]
    public bool EmulateBook { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.LayoutMode"/>
    [Required]
    public LayoutMode LayoutMode { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.BackgroundColor"/>
    [Required]
    public string BackgroundColor { get; set; } = "#000000";

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.SwipeToPaginate"/>
    [Required]
    public bool SwipeToPaginate { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.AllowAutomaticWebtoonReaderDetection"/>
    [Required]
    public bool AllowAutomaticWebtoonReaderDetection { get; set; }

    /// <inheritdoc cref="AppUserReadingProfile.WidthOverride"/>
    public int? WidthOverride { get; set; }

    /// <inheritdoc cref="AppUserReadingProfile.DisableWidthOverride"/>
    public BreakPoint DisableWidthOverride { get; set; } = BreakPoint.Never;

    #endregion

    #region EpubReader

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.BookReaderMargin"/>
    [Required]
    public int BookReaderMargin { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.BookReaderLineSpacing"/>
    [Required]
    public int BookReaderLineSpacing { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.BookReaderFontSize"/>
    [Required]
    public int BookReaderFontSize { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.BookReaderFontFamily"/>
    [Required]
    public string BookReaderFontFamily { get; set; } = null!;

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.BookReaderTapToPaginate"/>
    [Required]
    public bool BookReaderTapToPaginate { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.BookReaderReadingDirection"/>
    [Required]
    public ReadingDirection BookReaderReadingDirection { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.BookReaderWritingStyle"/>
    [Required]
    public WritingStyle BookReaderWritingStyle { get; set; }

    /// <inheritdoc cref="AppUserReadingProfile.BookThemeName"/>
    [Required]
    public string BookReaderThemeName { get; set; } = null!;

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.BookReaderLayoutMode"/>
    [Required]
    public BookPageLayoutMode BookReaderLayoutMode { get; set; }

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.BookReaderImmersiveMode"/>
    [Required]
    public bool BookReaderImmersiveMode { get; set; } = false;

    #endregion

    #region PdfReader

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.PdfTheme"/>
    [Required]
    public PdfTheme PdfTheme { get; set; } = PdfTheme.Dark;

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.PdfScrollMode"/>
    [Required]
    public PdfScrollMode PdfScrollMode { get; set; } = PdfScrollMode.Vertical;

    /// <inheritdoc cref="API.Entities.AppUserReadingProfile.PdfSpreadMode"/>
    [Required]
    public PdfSpreadMode PdfSpreadMode { get; set; } = PdfSpreadMode.None;

    #endregion

}
