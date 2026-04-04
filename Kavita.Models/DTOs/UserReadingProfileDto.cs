using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.User;

namespace Kavita.Models.DTOs;

public sealed record UserReadingProfileDto
{

    public int Id { get; set; }
    public int UserId { get; init; }

    public string Name { get; init; }
    public ReadingProfileKind Kind { get; init; }
    public List<int> DeviceIds { get; init; }
    public List<int> SeriesIds { get; init; }
    public List<int> LibraryIds { get; init; }

    #region MangaReader

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.ReadingDirection"/>
    [Required]
    public ReadingDirection ReadingDirection { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.ScalingOption"/>
    [Required]
    public ScalingOption ScalingOption { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.PageSplitOption"/>
    [Required]
    public PageSplitOption PageSplitOption { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.ReaderMode"/>
    [Required]
    public ReaderMode ReaderMode { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.AutoCloseMenu"/>
    [Required]
    public bool AutoCloseMenu { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.ShowScreenHints"/>
    [Required]
    public bool ShowScreenHints { get; set; } = true;

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.EmulateBook"/>
    [Required]
    public bool EmulateBook { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.LayoutMode"/>
    [Required]
    public LayoutMode LayoutMode { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BackgroundColor"/>
    [Required]
    public string BackgroundColor { get; set; } = "#000000";

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.SwipeToPaginate"/>
    [Required]
    public bool SwipeToPaginate { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.AllowAutomaticWebtoonReaderDetection"/>
    [Required]
    public bool AllowAutomaticWebtoonReaderDetection { get; set; }

    /// <inheritdoc cref="AppUserReadingProfile.WidthOverride"/>
    public int? WidthOverride { get; set; }

    /// <inheritdoc cref="AppUserReadingProfile.DisableWidthOverride"/>
    public BreakPoint DisableWidthOverride { get; set; } = BreakPoint.Never;

    #endregion

    #region EpubReader

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BookReaderMargin"/>
    [Required]
    public int BookReaderMargin { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BookReaderLineSpacing"/>
    [Required]
    public int BookReaderLineSpacing { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BookReaderFontSize"/>
    [Required]
    public int BookReaderFontSize { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BookReaderFontFamily"/>
    [Required]
    public string BookReaderFontFamily { get; set; } = null!;

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BookReaderTapToPaginate"/>
    [Required]
    public bool BookReaderTapToPaginate { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BookReaderReadingDirection"/>
    [Required]
    public ReadingDirection BookReaderReadingDirection { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BookReaderWritingStyle"/>
    [Required]
    public WritingStyle BookReaderWritingStyle { get; set; }

    /// <inheritdoc cref="AppUserReadingProfile.BookThemeName"/>
    [Required]
    public string BookReaderThemeName { get; set; } = null!;

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BookReaderLayoutMode"/>
    [Required]
    public BookPageLayoutMode BookReaderLayoutMode { get; set; }

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BookReaderImmersiveMode"/>
    [Required]
    public bool BookReaderImmersiveMode { get; set; } = false;

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.BookReaderImmersiveMode"/>
    [Required]
    public bool BookReaderDisableBookmarkIcon { get; set; } = false;

    #endregion

    #region PdfReader

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.PdfTheme"/>
    [Required]
    public PdfTheme PdfTheme { get; set; } = PdfTheme.Dark;

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.PdfScrollMode"/>
    [Required]
    public PdfScrollMode PdfScrollMode { get; set; } = PdfScrollMode.Vertical;

    /// <inheritdoc cref="Kavita.Models.Entities.User.AppUserReadingProfile.PdfSpreadMode"/>
    [Required]
    public PdfSpreadMode PdfSpreadMode { get; set; } = PdfSpreadMode.None;

    #endregion

}
