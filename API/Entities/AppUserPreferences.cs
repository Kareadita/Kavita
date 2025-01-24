using API.Data;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;

namespace API.Entities;

public class AppUserPreferences
{
    public int Id { get; set; }

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
    public string BookReaderFontFamily { get; set; } = "default";
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

    #region Global

    /// <summary>
    /// UI Site Global Setting: The UI theme the user should use.
    /// </summary>
    /// <remarks>Should default to Dark</remarks>
    public required SiteTheme Theme { get; set; } = Seed.DefaultThemes[0];
    /// <summary>
    /// Global Site Option: If the UI should layout items as Cards or List items
    /// </summary>
    /// <remarks>Defaults to Cards</remarks>
    public PageLayoutMode GlobalPageLayoutMode { get; set; } = PageLayoutMode.Cards;
    /// <summary>
    /// UI Site Global Setting: If unread summaries should be blurred until expanded or unless user has read it already
    /// </summary>
    /// <remarks>Defaults to false</remarks>
    public bool BlurUnreadSummaries { get; set; } = false;
    /// <summary>
    /// UI Site Global Setting: Should Kavita prompt user to confirm downloads that are greater than 100 MB.
    /// </summary>
    public bool PromptForDownloadSize { get; set; } = true;
    /// <summary>
    /// UI Site Global Setting: Should Kavita disable CSS transitions
    /// </summary>
    public bool NoTransitions { get; set; } = false;
    /// <summary>
    /// UI Site Global Setting: When showing series, only parent series or series with no relationships will be returned
    /// </summary>
    public bool CollapseSeriesRelationships { get; set; } = false;
    /// <summary>
    /// UI Site Global Setting: Should series reviews be shared with all users in the server
    /// </summary>
    public bool ShareReviews { get; set; } = false;
    /// <summary>
    /// UI Site Global Setting: The language locale that should be used for the user
    /// </summary>
    public string Locale { get; set; }
    #endregion

    #region KavitaPlus
    /// <summary>
    /// Should this account have Scrobbling enabled for AniList
    /// </summary>
    public bool AniListScrobblingEnabled { get; set; }
    /// <summary>
    /// Should this account have Want to Read Sync enabled
    /// </summary>
    public bool WantToReadSync { get; set; }
    #endregion

    public AppUser AppUser { get; set; } = null!;
    public int AppUserId { get; set; }
}
