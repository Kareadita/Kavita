using System.Collections.Generic;
using System.ComponentModel;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;
using API.Services.Tasks;

namespace API.Entities;

public enum BreakPoint
{
    [Description("Never")]
    Never = 0,
    [Description("Mobile")]
    Mobile = 1,
    [Description("Tablet")]
    Tablet = 2,
    [Description("Desktop")]
    Desktop = 3,
}

public class AppUserReadingProfile
{
    public int Id { get; set; }

    public string Name { get; set; }
    public string NormalizedName { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; }

    public ReadingProfileKind Kind { get; set; }
    public List<int> LibraryIds { get; set; }
    public List<int> SeriesIds { get; set; }

    #region MangaReader

    /// <summary>
    /// Manga Reader Option: What direction should the next/prev page buttons go
    /// </summary>
    public ReadingDirection ReadingDirection { get; set; } = ReadingDirection.LeftToRight;
    /// <summary>
    /// Manga Reader Option: How should the image be scaled to screen
    /// </summary>
    public ScalingOption ScalingOption { get; set; } = ScalingOption.Automatic;
    /// <summary>
    /// Manga Reader Option: Which side of a split image should we show first
    /// </summary>
    public PageSplitOption PageSplitOption { get; set; } = PageSplitOption.FitSplit;
    /// <summary>
    /// Manga Reader Option: How the manga reader should perform paging or reading of the file
    /// <example>
    /// Webtoon uses scrolling to page, MANGA_LR uses paging by clicking left/right side of reader, MANGA_UD uses paging
    /// by clicking top/bottom sides of reader.
    /// </example>
    /// </summary>
    public ReaderMode ReaderMode { get; set; }
    /// <summary>
    /// Manga Reader Option: Allow the menu to close after 6 seconds without interaction
    /// </summary>
    public bool AutoCloseMenu { get; set; } = true;
    /// <summary>
    /// Manga Reader Option: Show screen hints to the user on some actions, ie) pagination direction change
    /// </summary>
    public bool ShowScreenHints { get; set; } = true;
    /// <summary>
    /// Manga Reader Option: Emulate a book by applying a shadow effect on the pages
    /// </summary>
    public bool EmulateBook { get; set; } = false;
    /// <summary>
    /// Manga Reader Option: How many pages to display in the reader at once
    /// </summary>
    public LayoutMode LayoutMode { get; set; } = LayoutMode.Single;
    /// <summary>
    /// Manga Reader Option: Background color of the reader
    /// </summary>
    public string BackgroundColor { get; set; } = "#000000";
    /// <summary>
    /// Manga Reader Option: Should swiping trigger pagination
    /// </summary>
    public bool SwipeToPaginate { get; set; }
    /// <summary>
    /// Manga Reader Option: Allow Automatic Webtoon detection
    /// </summary>
    public bool AllowAutomaticWebtoonReaderDetection { get; set; }
    /// <summary>
    /// Manga Reader Option: Optional fixed width override
    /// </summary>
    public int? WidthOverride { get; set; } = null;
    /// <summary>
    /// Manga Reader Option: Disable the width override if the screen is past the breakpoint
    /// </summary>
    public BreakPoint DisableWidthOverride { get; set; } = BreakPoint.Never;

    #endregion

    #region EpubReader

    /// <summary>
    /// Book Reader Option: Override extra Margin
    /// </summary>
    public int BookReaderMargin { get; set; } = 15;
    /// <summary>
    /// Book Reader Option: Override line-height
    /// </summary>
    public int BookReaderLineSpacing { get; set; } = 100;
    /// <summary>
    /// Book Reader Option: Override font size
    /// </summary>
    public int BookReaderFontSize { get; set; } = 100;
    /// <summary>
    /// Book Reader Option: Maps to the default Kavita font-family (inherit) or an override
    /// </summary>
    public string BookReaderFontFamily { get; set; } = FontService.DefaultFont;
    /// <summary>
    /// Book Reader Option: Allows tapping on side of screens to paginate
    /// </summary>
    public bool BookReaderTapToPaginate { get; set; } = false;
    /// <summary>
    /// Book Reader Option: What direction should the next/prev page buttons go
    /// </summary>
    public ReadingDirection BookReaderReadingDirection { get; set; } = ReadingDirection.LeftToRight;
    /// <summary>
    /// Book Reader Option: Defines the writing styles vertical/horizontal
    /// </summary>
    public WritingStyle BookReaderWritingStyle { get; set; } = WritingStyle.Horizontal;
    /// <summary>
    /// Book Reader Option: The color theme to decorate the book contents
    /// </summary>
    /// <remarks>Should default to Dark</remarks>
    public string BookThemeName { get; set; } = "Dark";
    /// <summary>
    /// Book Reader Option: The way a page from a book is rendered. Default is as book dictates, 1 column is fit to height,
    /// 2 column is fit to height, 2 columns
    /// </summary>
    /// <remarks>Defaults to Default</remarks>
    public BookPageLayoutMode BookReaderLayoutMode { get; set; } = BookPageLayoutMode.Default;
    /// <summary>
    /// Book Reader Option: A flag that hides the menu-ing system behind a click on the screen. This should be used with tap to paginate, but the app doesn't enforce this.
    /// </summary>
    /// <remarks>Defaults to false</remarks>
    public bool BookReaderImmersiveMode { get; set; } = false;
    #endregion

    #region PdfReader

    /// <summary>
    /// PDF Reader: Theme of the Reader
    /// </summary>
    public PdfTheme PdfTheme { get; set; } = PdfTheme.Dark;
    /// <summary>
    /// PDF Reader: Scroll mode of the reader
    /// </summary>
    public PdfScrollMode PdfScrollMode { get; set; } = PdfScrollMode.Vertical;
    /// <summary>
    /// PDF Reader: Spread Mode of the reader
    /// </summary>
    public PdfSpreadMode PdfSpreadMode { get; set; } = PdfSpreadMode.None;


    #endregion
}
