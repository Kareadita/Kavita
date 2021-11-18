using API.Entities.Enums;

namespace API.Entities
{
    public class AppUserPreferences
    {
        public int Id { get; set; }
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
        /// Book Reader Option: Should the background color be dark
        /// </summary>
        public bool BookReaderDarkMode { get; set; } = true;
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
        /// UI Site Global Setting: Whether the UI should render in Dark mode or not.
        /// </summary>
        public bool SiteDarkMode { get; set; } = true;



        public AppUser AppUser { get; set; }
        public int AppUserId { get; set; }
    }
}
