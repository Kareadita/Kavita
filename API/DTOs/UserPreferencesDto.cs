using System.ComponentModel.DataAnnotations;
using API.DTOs.Theme;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;

namespace API.DTOs;

public class UserPreferencesDto
{
    /// <summary>
    /// Manga Reader Option: What direction should the next/prev page buttons go
    /// </summary>
    [Required]
    public ReadingDirection ReadingDirection { get; set; }
    /// <summary>
    /// Manga Reader Option: How should the image be scaled to screen
    /// </summary>
    [Required]
    public ScalingOption ScalingOption { get; set; }
    /// <summary>
    /// Manga Reader Option: Which side of a split image should we show first
    /// </summary>
    [Required]
    public PageSplitOption PageSplitOption { get; set; }
    /// <summary>
    /// Manga Reader Option: How the manga reader should perform paging or reading of the file
    /// <example>
    /// Webtoon uses scrolling to page, LeftRight uses paging by clicking left/right side of reader, UpDown uses paging
    /// by clicking top/bottom sides of reader.
    /// </example>
    /// </summary>
    [Required]
    public ReaderMode ReaderMode { get; set; }
    /// <summary>
    /// Manga Reader Option: How many pages to display in the reader at once
    /// </summary>
    [Required]
    public LayoutMode LayoutMode { get; set; }
    /// <summary>
    /// Manga Reader Option: Emulate a book by applying a shadow effect on the pages
    /// </summary>
    [Required]
    public bool EmulateBook { get; set; }
    /// <summary>
    /// Manga Reader Option: Background color of the reader
    /// </summary>
    [Required]
    public string BackgroundColor { get; set; } = "#000000";
    /// <summary>
    /// Manga Reader Option: Should swiping trigger pagination
    /// </summary>
    [Required]
    public bool SwipeToPaginate { get; set; }
    /// <summary>
    /// Manga Reader Option: Allow the menu to close after 6 seconds without interaction
    /// </summary>
    [Required]
    public bool AutoCloseMenu { get; set; }
    /// <summary>
    /// Manga Reader Option: Show screen hints to the user on some actions, ie) pagination direction change
    /// </summary>
    [Required]
    public bool ShowScreenHints { get; set; } = true;
    /// <summary>
    /// Book Reader Option: Override extra Margin
    /// </summary>
    [Required]
    public int BookReaderMargin { get; set; }
    /// <summary>
    /// Book Reader Option: Override line-height
    /// </summary>
    [Required]
    public int BookReaderLineSpacing { get; set; }
    /// <summary>
    /// Book Reader Option: Override font size
    /// </summary>
    [Required]
    public int BookReaderFontSize { get; set; }
    /// <summary>
    /// Book Reader Option: Maps to the default Kavita font-family (inherit) or an override
    /// </summary>
    [Required]
    public string BookReaderFontFamily { get; set; } = null!;

    /// <summary>
    /// Book Reader Option: Allows tapping on side of screens to paginate
    /// </summary>
    [Required]
    public bool BookReaderTapToPaginate { get; set; }
    /// <summary>
    /// Book Reader Option: What direction should the next/prev page buttons go
    /// </summary>
    [Required]
    public ReadingDirection BookReaderReadingDirection { get; set; }

    /// <summary>
    /// Book Reader Option: What writing style should be used, horizontal or vertical.
    /// </summary>
    [Required]
    public WritingStyle BookReaderWritingStyle { get; set; }

    /// <summary>
    /// UI Site Global Setting: The UI theme the user should use.
    /// </summary>
    /// <remarks>Should default to Dark</remarks>
    [Required]
    public SiteThemeDto? Theme { get; set; }

    [Required] public string BookReaderThemeName { get; set; } = null!;
    [Required]
    public BookPageLayoutMode BookReaderLayoutMode { get; set; }
    /// <summary>
    /// Book Reader Option: A flag that hides the menu-ing system behind a click on the screen. This should be used with tap to paginate, but the app doesn't enforce this.
    /// </summary>
    /// <remarks>Defaults to false</remarks>
    [Required]
    public bool BookReaderImmersiveMode { get; set; } = false;
    /// <summary>
    /// Global Site Option: If the UI should layout items as Cards or List items
    /// </summary>
    /// <remarks>Defaults to Cards</remarks>
    [Required]
    public PageLayoutMode GlobalPageLayoutMode { get; set; } = PageLayoutMode.Cards;
    /// <summary>
    /// UI Site Global Setting: If unread summaries should be blurred until expanded or unless user has read it already
    /// </summary>
    /// <remarks>Defaults to false</remarks>
    [Required]
    public bool BlurUnreadSummaries { get; set; } = false;
    /// <summary>
    /// UI Site Global Setting: Should Kavita prompt user to confirm downloads that are greater than 100 MB.
    /// </summary>
    [Required]
    public bool PromptForDownloadSize { get; set; } = true;
    /// <summary>
    /// UI Site Global Setting: Should Kavita disable CSS transitions
    /// </summary>
    [Required]
    public bool NoTransitions { get; set; } = false;
    /// <summary>
    /// When showing series, only parent series or series with no relationships will be returned
    /// </summary>
    [Required]
    public bool CollapseSeriesRelationships { get; set; } = false;
    /// <summary>
    /// UI Site Global Setting: Should series reviews be shared with all users in the server
    /// </summary>
    [Required]
    public bool ShareReviews { get; set; } = false;
    /// <summary>
    /// UI Site Global Setting: The language locale that should be used for the user
    /// </summary>
    [Required]
    public string Locale { get; set; }

    /// <summary>
    /// PDF Reader: Theme of the Reader
    /// </summary>
    [Required]
    public PdfTheme PdfTheme { get; set; } = PdfTheme.Dark;
    /// <summary>
    /// PDF Reader: Scroll mode of the reader
    /// </summary>
    [Required]
    public PdfScrollMode PdfScrollMode { get; set; } = PdfScrollMode.Vertical;
    /// <summary>
    /// PDF Reader: Layout Mode of the reader
    /// </summary>
    [Required]
    public PdfLayoutMode PdfLayoutMode { get; set; } = PdfLayoutMode.Multiple;
    /// <summary>
    /// PDF Reader: Spread Mode of the reader
    /// </summary>
    [Required]
    public PdfSpreadMode PdfSpreadMode { get; set; } = PdfSpreadMode.None;

    /// <summary>
    /// Kavita+: Should this account have Scrobbling enabled for AniList
    /// </summary>
    public bool AniListScrobblingEnabled { get; set; }
    /// <summary>
    /// Kavita+: Should this account have Want to Read Sync enabled
    /// </summary>
    public bool WantToReadSync { get; set; }
}
