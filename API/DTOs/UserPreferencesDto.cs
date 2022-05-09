using API.DTOs.Theme;
using API.Entities;
using API.Entities.Enums;

namespace API.DTOs
{
    public class UserPreferencesDto
    {
        /// <summary>
        /// Manga Reader Option: What direction should the next/prev page buttons go
        /// </summary>
        public ReadingDirection ReadingDirection { get; set; }
        /// <summary>
        /// Manga Reader Option: How should the image be scaled to screen
        /// </summary>
        public ScalingOption ScalingOption { get; set; }
        /// <summary>
        /// Manga Reader Option: Which side of a split image should we show first
        /// </summary>
        public PageSplitOption PageSplitOption { get; set; }
        /// <summary>
        /// Manga Reader Option: How the manga reader should perform paging or reading of the file
        /// <example>
        /// Webtoon uses scrolling to page, LeftRight uses paging by clicking left/right side of reader, UpDown uses paging
        /// by clicking top/bottom sides of reader.
        /// </example>
        /// </summary>
        public ReaderMode ReaderMode { get; set; }
        /// <summary>
        /// Manga Reader Option: How many pages to display in the reader at once
        /// </summary>
        public LayoutMode LayoutMode { get; set; }
        /// <summary>
        /// Manga Reader Option: Background color of the reader
        /// </summary>
        public string BackgroundColor { get; set; } = "#000000";
        /// <summary>
        /// Manga Reader Option: Allow the menu to close after 6 seconds without interaction
        /// </summary>
        public bool AutoCloseMenu { get; set; }
        /// <summary>
        /// Manga Reader Option: Show screen hints to the user on some actions, ie) pagination direction change
        /// </summary>
        public bool ShowScreenHints { get; set; } = true;
        /// <summary>
        /// Book Reader Option: Should the background color be dark
        /// </summary>
        public bool BookReaderDarkMode { get; set; } = false;
        /// <summary>
        /// Book Reader Option: Override extra Margin
        /// </summary>
        public int BookReaderMargin { get; set; }
        /// <summary>
        /// Book Reader Option: Override line-height
        /// </summary>
        public int BookReaderLineSpacing { get; set; }
        /// <summary>
        /// Book Reader Option: Override font size
        /// </summary>
        public int BookReaderFontSize { get; set; }
        /// <summary>
        /// Book Reader Option: Maps to the default Kavita font-family (inherit) or an override
        /// </summary>
        public string BookReaderFontFamily { get; set; }
        /// <summary>
        /// Book Reader Option: Allows tapping on side of screens to paginate
        /// </summary>
        public bool BookReaderTapToPaginate { get; set; }
        /// <summary>
        /// Book Reader Option: What direction should the next/prev page buttons go
        /// </summary>
        public ReadingDirection BookReaderReadingDirection { get; set; }
        /// <summary>
        /// UI Site Global Setting: The UI theme the user should use.
        /// </summary>
        /// <remarks>Should default to Dark</remarks>
        public SiteTheme Theme { get; set; }
        public string BookReaderThemeName { get; set; }
        public BookPageLayoutMode BookReaderLayoutMode { get; set; }
    }
}
